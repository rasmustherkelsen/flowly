using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Flowly.Services;

namespace Flowly.Tool.QueueDiscovery;

internal static class HostBasedQueueDiscovery
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan DefaultMessageTimeToLive = TimeSpan.FromDays(1);
    private static readonly TimeSpan DefaultLockDuration = TimeSpan.FromMinutes(5);
    private const bool DefaultDeadLetterOnMessageExpiration = true;

    public static (IReadOnlyList<QueueDiscoveryQueue> Queues, IReadOnlyList<QueueDiscoveryEvent> Events) Discover(string assemblyPath, string workingDirectory, string? providerNameFilter = null)
    {
        var outputFile = Path.GetTempFileName();

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{assemblyPath}\"",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            processStartInfo.Environment[CommandLineParserHostedServiceDefinitions.OutputFileEnvVar] = outputFile;

            using var process = Process.Start(processStartInfo)
                ?? throw new InvalidOperationException("Could not start process for host-based queue discovery.");

            var stderr = new StringBuilder();
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var exited = process.WaitForExit((int)ProcessTimeout.TotalMilliseconds);

            if (exited)
            {
                process.WaitForExit();
            }
            else
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException(
                    $"Host-based queue discovery timed out after {ProcessTimeout.TotalSeconds}s. " +
                    "Ensure the application does not block indefinitely during startup.");
            }

            if (IsNoTransportProviderError(stderr.ToString()))
                throw new MissingTransportProviderException(
                    "No transport provider has been configured. " +
                    "Add a transport provider (e.g. UseAzureServiceBus()) in your IFlowlyConfiguration.");

            if (new FileInfo(outputFile).Length == 0)
            {
                throw new InvalidOperationException(
                    "Host-based queue discovery produced no output. " +
                    "Ensure the application uses AddFlowly() and can start without required external services.\n" +
                    (stderr.Length > 0 ? stderr.ToString() : "(no stderr output)"));
            }

            return ParseOutput(outputFile, providerNameFilter);
        }
        finally
        {
            File.Delete(outputFile);
        }
    }

    private static (IReadOnlyList<QueueDiscoveryQueue> Queues, IReadOnlyList<QueueDiscoveryEvent> Events) ParseOutput(string outputFile, string? providerNameFilter)
    {
        var json = File.ReadAllText(outputFile);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var output = JsonSerializer.Deserialize<HostDiscoveryOutput>(json, options)
            ?? new HostDiscoveryOutput([], []);

        var queues = ParseQueues(output.Queues ?? [], providerNameFilter);
        var events = ParseEvents(output.Events ?? [], providerNameFilter);

        return (queues, events);
    }

    private static IReadOnlyList<QueueDiscoveryQueue> ParseQueues(HostDiscoveredQueue[] entries, string? providerNameFilter)
    {
        var filtered = providerNameFilter is null
            ? entries.Where(e => e.IsPrimary)
            : entries.Where(e => string.Equals(e.ProviderName, providerNameFilter, StringComparison.OrdinalIgnoreCase));

        return filtered
            .Where(e => !string.IsNullOrWhiteSpace(e.QueueName))
            .GroupBy(e => e.QueueName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();

                var defaultMessageTimeToLive = ResolveConsistentValue(
                    group.Select(e => e.DefaultMessageTimeToLive is not null ? TimeSpan.Parse(e.DefaultMessageTimeToLive) : (TimeSpan?)null),
                    DefaultMessageTimeToLive,
                    first.QueueName,
                    nameof(HostDiscoveredQueue.DefaultMessageTimeToLive));

                var deadLetterOnMessageExpiration = ResolveConsistentValue(
                    group.Select(e => e.DeadLetterOnMessageExpiration),
                    DefaultDeadLetterOnMessageExpiration,
                    first.QueueName,
                    nameof(HostDiscoveredQueue.DeadLetterOnMessageExpiration));

                var lockDuration = ResolveConsistentValue(
                    group.Select(e => e.LockDuration is not null ? TimeSpan.Parse(e.LockDuration) : (TimeSpan?)null),
                    DefaultLockDuration,
                    first.QueueName,
                    nameof(HostDiscoveredQueue.LockDuration));

                return new QueueDiscoveryQueue(
                    first.QueueName,
                    first.ProviderName,
                    group.Any(e => e.RequiresSession),
                    defaultMessageTimeToLive,
                    deadLetterOnMessageExpiration,
                    lockDuration);
            })
            .OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<QueueDiscoveryEvent> ParseEvents(HostDiscoveredEvent[] entries, string? providerNameFilter)
    {
        var filtered = providerNameFilter is null
            ? entries.Where(e => e.IsPrimary)
            : entries.Where(e => string.Equals(e.ProviderName, providerNameFilter, StringComparison.OrdinalIgnoreCase));

        return filtered
            .GroupBy(
                e => $"{e.TopicName.ToLowerInvariant()}|{e.SubscriptionName.ToLowerInvariant()}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();

                var defaultMessageTimeToLive = ResolveConsistentValue(
                    group.Select(e => e.DefaultMessageTimeToLive is not null ? TimeSpan.Parse(e.DefaultMessageTimeToLive) : (TimeSpan?)null),
                    DefaultMessageTimeToLive,
                    first.TopicName,
                    nameof(HostDiscoveredEvent.DefaultMessageTimeToLive));

                var deadLetterOnMessageExpiration = ResolveConsistentValue(
                    group.Select(e => e.DeadLetterOnMessageExpiration),
                    DefaultDeadLetterOnMessageExpiration,
                    first.TopicName,
                    nameof(HostDiscoveredEvent.DeadLetterOnMessageExpiration));

                return new QueueDiscoveryEvent(
                    first.TopicName,
                    first.SubscriptionName,
                    first.ProviderName,
                    defaultMessageTimeToLive,
                    deadLetterOnMessageExpiration);
            })
            .OrderBy(e => e.TopicName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.SubscriptionName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static T ResolveConsistentValue<T>(IEnumerable<T?> values, T defaultValue, string queueName, string settingName)
        where T : struct
    {
        var concreteValues = values
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .Distinct()
            .ToArray();

        if (concreteValues.Length == 0) return defaultValue;
        if (concreteValues.Length == 1) return concreteValues[0];

        throw new InvalidOperationException($"Conflicting queue setting '{settingName}' for queue '{queueName}'.");
    }

    private static bool IsNoTransportProviderError(string stderr) =>
        stderr.Contains("No transport provider", StringComparison.OrdinalIgnoreCase);

    private sealed record HostDiscoveryOutput(
        HostDiscoveredQueue[]? Queues,
        HostDiscoveredEvent[]? Events);

    private sealed record HostDiscoveredQueue(
        string QueueName,
        string ProviderName,
        bool IsPrimary,
        bool RequiresSession,
        string? DefaultMessageTimeToLive,
        bool? DeadLetterOnMessageExpiration,
        string? LockDuration);

    private sealed record HostDiscoveredEvent(
        string TopicName,
        string SubscriptionName,
        string ProviderName,
        bool IsPrimary,
        string? DefaultMessageTimeToLive,
        bool? DeadLetterOnMessageExpiration);
}
