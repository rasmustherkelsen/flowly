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

    public static IReadOnlyList<QueueDiscoveryQueue> DiscoverQueues(string assemblyPath, string workingDirectory, string? providerNameFilter = null)
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

    private static IReadOnlyList<QueueDiscoveryQueue> ParseOutput(string outputFile, string? providerNameFilter)
    {
        var json = File.ReadAllText(outputFile);
        var entries = JsonSerializer.Deserialize<HostDiscoveredQueue[]>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

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

    private sealed record HostDiscoveredQueue(
        string QueueName,
        string ProviderName,
        bool IsPrimary,
        bool RequiresSession,
        string? DefaultMessageTimeToLive,
        bool? DeadLetterOnMessageExpiration,
        string? LockDuration);
}
