using System.Text.Json;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Hosting;

namespace Flowly.Services;

public static class CommandLineParserHostedServiceDefinitions
{
    public const string OutputFileEnvVar = "FLOWLY_DISCOVER_QUEUES_OUTPUT";
}

internal class CommandLineParserHostedService(IEnumerable<DeferredQueueRegistration> queueRegistrations, IHostApplicationLifetime lifetime)
    : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        var outputPath = Environment.GetEnvironmentVariable(CommandLineParserHostedServiceDefinitions.OutputFileEnvVar);
        if (outputPath is null) return Task.CompletedTask;

        var entries = queueRegistrations.Select(r => new QueueDiscoveryEntry(
            r.QueueName,
            r.RequiresSession,
            r.DefaultMessageTimeToLive?.ToString("c"),
            r.DeadLetterOnMessageExpiration,
            r.LockDuration?.ToString("c"))).ToArray();

        File.WriteAllText(outputPath, JsonSerializer.Serialize(entries));
        lifetime.StopApplication();

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken _) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken _) => Task.CompletedTask;

    public Task StopAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken _) => Task.CompletedTask;

    private sealed record QueueDiscoveryEntry(
        string QueueName,
        bool RequiresSession,
        string? DefaultMessageTimeToLive,
        bool? DeadLetterOnMessageExpiration,
        string? LockDuration);
}