using Flowly.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.Registration;

internal class QueueRegistrarHostedService(
    IEnumerable<ProviderQueueManifest> manifests,
    IMessageBusClientRegistry clientRegistry,
    IMessagingTopologyCreatorRegistry topologyRegistry,
    IEventTopologyCreatorRegistry eventTopologyRegistry,
    IEnumerable<IMessagingTopologyValidator> topologyValidators,
    ICrossProviderConflictValidator conflictValidator,
    FlowlyOptions flowlyOptions,
    ILogger<QueueRegistrarHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!clientRegistry.GetAll().Any())
            throw new InvalidOperationException(
                "No transport provider has been registered. " +
                "Call a transport provider extension (e.g. UseAzureServiceBus()) in your IFlowlyConfiguration before registering handlers or submitters.");

        conflictValidator.Validate(manifests);

        foreach (var manifest in manifests)
        {
            var transport = clientRegistry.GetAll()
                .FirstOrDefault(t => string.Equals(t.Name, manifest.ProviderName, StringComparison.OrdinalIgnoreCase));

            var createTopology = transport?.CreateTopologyOverride ?? flowlyOptions.CreateTopology;
            var queueDescriptions = ResolveQueueDescriptions(manifest);

            if (!createTopology)
            {
                logger.LogDebug("Skipping topology creation for provider '{Provider}'", manifest.ProviderName);

                var validator = topologyValidators.FirstOrDefault(v =>
                    string.Equals(v.ProviderName, manifest.ProviderName, StringComparison.OrdinalIgnoreCase));

                if (validator != null)
                    await validator.Validate(queueDescriptions, cancellationToken);

                continue;
            }

            logger.LogDebug("Creating messaging topology for provider '{Provider}'", manifest.ProviderName);

            var topologyCreator = topologyRegistry.GetCreator(manifest.ProviderName);
            await topologyCreator.CreateTopology(queueDescriptions, cancellationToken);

            var eventDescriptions = ResolveEventDescriptions(manifest);
            if (eventDescriptions.Count > 0)
            {
                var eventTopologyCreator = eventTopologyRegistry.TryGetCreator(manifest.ProviderName);
                if (eventTopologyCreator != null)
                    await eventTopologyCreator.CreateEventTopology(eventDescriptions, cancellationToken);
                else
                    logger.LogWarning("Provider '{Provider}' has event registrations but no event topology creator is registered.", manifest.ProviderName);
            }

            logger.LogInformation("Messaging topology created for provider '{Provider}'", manifest.ProviderName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static IReadOnlyList<IQueueDescription> ResolveQueueDescriptions(ProviderQueueManifest manifest)
    {
        return manifest.Queues
            .Select(r => (IQueueDescription)new QueueDescription(
                r.QueueName,
                r.DefaultMessageTimeToLive ?? TimeSpan.FromDays(1),
                r.DeadLetterOnMessageExpiration ?? true,
                r.LockDuration ?? TimeSpan.FromMinutes(5),
                r.RequiresSession))
            .ToList();
    }

    private static IReadOnlyList<IEventDescription> ResolveEventDescriptions(ProviderQueueManifest manifest)
    {
        return manifest.Events
            .Select<Events.Registration.DeferredEventRegistration, IEventDescription>(e => e.SubscriptionName is not null
                ? new EventDescription(
                    e.TopicName,
                    e.SubscriptionName,
                    e.DefaultMessageTimeToLive ?? TimeSpan.FromDays(1),
                    e.DeadLetterOnMessageExpiration ?? true)
                : new EventTopicDescription(
                    e.TopicName,
                    e.DefaultMessageTimeToLive ?? TimeSpan.FromDays(1),
                    e.DeadLetterOnMessageExpiration ?? true))
            .ToList();
    }
}