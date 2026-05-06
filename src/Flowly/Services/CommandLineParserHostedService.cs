using System.Text.Json;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Hosting;

namespace Flowly.Services;

/// <summary>
///     Exposes constants used by the design-time queue discovery mechanism. When the
///     <see cref="OutputFileEnvVar" /> environment variable is set, the Flowly CLI tool starts the application in a
///     headless mode that writes a JSON discovery manifest to the specified path and immediately exits — without
///     connecting to any message broker.
/// </summary>
public static class CommandLineParserHostedServiceDefinitions
{
    /// <summary>
    ///     The name of the environment variable (<c>FLOWLY_DISCOVER_QUEUES_OUTPUT</c>) that, when set to a file path,
    ///     causes the application to write a queue discovery manifest to that path and exit. Used exclusively by the
    ///     <c>flowly</c> CLI tool.
    /// </summary>
    public const string OutputFileEnvVar = "FLOWLY_DISCOVER_QUEUES_OUTPUT";
}

internal class CommandLineParserHostedService(IEnumerable<ProviderQueueManifest> manifests, IHostApplicationLifetime lifetime) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        var outputPath = Environment.GetEnvironmentVariable(CommandLineParserHostedServiceDefinitions.OutputFileEnvVar);
        if (outputPath is null) return Task.CompletedTask;

        var queueEntries = manifests
            .SelectMany(m => m.Queues.Select(r => new QueueDiscoveryEntry(
                r.QueueName,
                m.ProviderName,
                m.IsPrimary,
                r.RequiresSession,
                r.DefaultMessageTimeToLive?.ToString("c"),
                r.DeadLetterOnMessageExpiration,
                r.LockDuration?.ToString("c"))))
            .ToArray();

        var eventEntries = manifests
            .SelectMany(m => m.Events.Select(e => new EventDiscoveryEntry(
                e.TopicName,
                e.SubscriptionName!,
                m.ProviderName,
                m.IsPrimary,
                e.DefaultMessageTimeToLive?.ToString("c"),
                e.DeadLetterOnMessageExpiration)))
            .ToArray();

        var output = new DiscoveryOutput(queueEntries, eventEntries);

        File.WriteAllText(outputPath, JsonSerializer.Serialize(output));
        lifetime.StopApplication();

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken _) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken _) => Task.CompletedTask;

    public Task StopAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken _) => Task.CompletedTask;

    private sealed record DiscoveryOutput(
        QueueDiscoveryEntry[] Queues,
        EventDiscoveryEntry[] Events);

    private sealed record QueueDiscoveryEntry(
        string QueueName,
        string ProviderName,
        bool IsPrimary,
        bool RequiresSession,
        string? DefaultMessageTimeToLive,
        bool? DeadLetterOnMessageExpiration,
        string? LockDuration);

    private sealed record EventDiscoveryEntry(
        string TopicName,
        string SubscriptionName,
        string ProviderName,
        bool IsPrimary,
        string? DefaultMessageTimeToLive,
        bool? DeadLetterOnMessageExpiration);
}
