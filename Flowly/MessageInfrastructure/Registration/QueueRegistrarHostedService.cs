using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.Registration;

internal class QueueRegistrarHostedService(
    IEnumerable<ProviderQueueManifest> manifests,
    IMessageBusClientRegistry clientRegistry,
    IMessagingTopologyCreatorRegistry topologyRegistry,
    ICrossProviderConflictValidator conflictValidator,
    FlowlyOptions flowlyOptions,
    ILogger<QueueRegistrarHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        conflictValidator.Validate(manifests);

        foreach (var manifest in manifests)
        {
            var transport = clientRegistry.GetAll()
                .FirstOrDefault(t => string.Equals(t.Name, manifest.ProviderName, StringComparison.OrdinalIgnoreCase));

            var createTopology = transport?.CreateTopologyOverride ?? flowlyOptions.CreateTopology;

            if (!createTopology)
            {
                logger.LogDebug("Skipping topology creation for provider '{Provider}'", manifest.ProviderName);
                continue;
            }

            logger.LogDebug("Creating messaging topology for provider '{Provider}'", manifest.ProviderName);

            var topologyCreator = topologyRegistry.GetCreator(manifest.ProviderName);
            var queueDescriptions = ResolveQueueDescriptions(manifest);
            await topologyCreator.CreateTopology(queueDescriptions, cancellationToken);

            logger.LogInformation("Messaging topology created for provider '{Provider}'", manifest.ProviderName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IReadOnlyList<IQueueDescription> ResolveQueueDescriptions(ProviderQueueManifest manifest) =>
        manifest.Queues
            .Select(r => (IQueueDescription)new QueueDescription(
                r.QueueName,
                r.DefaultMessageTimeToLive ?? TimeSpan.FromDays(1),
                r.DeadLetterOnMessageExpiration ?? true,
                r.LockDuration ?? TimeSpan.FromMinutes(5),
                r.RequiresSession))
            .ToList();
}
