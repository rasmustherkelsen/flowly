using Azure.Core;
using Azure.Messaging.EventHubs.Primitives;

namespace Flowly.AzureEventHubs;

internal sealed class EventHubsProcessorFactory(
    Func<EventHubsProcessorRequest, IEventHubsProcessor> createProcessor,
    EventHubsOwnershipOptions ownershipOptions,
    TimeProvider? timeProvider = null) : IEventHubsProcessorFactory
{
    public static EventHubsProcessorFactory ForConnectionString(string connectionString, EventHubsOwnershipOptions ownershipOptions)
        => new(request => EventHubsSdkProcessor.ForConnectionString(EventHubsClientFactory.RejectEntityPath(connectionString), request), ownershipOptions);

    public static EventHubsProcessorFactory ForCredential(string fullyQualifiedNamespace, TokenCredential credential, EventHubsOwnershipOptions ownershipOptions)
        => new(request => EventHubsSdkProcessor.ForCredential(fullyQualifiedNamespace, credential, request), ownershipOptions);

    public IEventHubsProcessor Create(string eventHubName, string consumerGroup, IEventHubsPartitionCallbacks callbacks)
    {
        var processorOptions = ownershipOptions.ApplyTo(new EventProcessorOptions { Identifier = Guid.NewGuid().ToString("N") });

        return createProcessor(new EventHubsProcessorRequest(
            eventHubName,
            consumerGroup,
            CreateStore(processorOptions, callbacks),
            processorOptions,
            callbacks));
    }

    private CheckpointStore CreateStore(EventProcessorOptions processorOptions, IEventHubsPartitionCallbacks callbacks)
        => ownershipOptions.IsShared
            ? new OwnershipObservingCheckpointStore(
                ownershipOptions.CreateStore(),
                processorOptions.Identifier!,
                processorOptions.PartitionOwnershipExpirationInterval,
                callbacks,
                timeProvider ?? TimeProvider.System)
            : ownershipOptions.CreateStore();
}
