using Azure.Core;
using Azure.Messaging.EventHubs.Primitives;

namespace Flowly.AzureEventHubs;

internal sealed class EventHubsProcessorFactory(Func<EventHubsProcessorRequest, IEventHubsProcessor> createProcessor, EventHubsOwnershipOptions ownershipOptions) : IEventHubsProcessorFactory
{
    public static EventHubsProcessorFactory ForConnectionString(string connectionString, EventHubsOwnershipOptions ownershipOptions)
        => new(request => EventHubsSdkProcessor.ForConnectionString(EventHubsClientFactory.RejectEntityPath(connectionString), request), ownershipOptions);

    public static EventHubsProcessorFactory ForCredential(string fullyQualifiedNamespace, TokenCredential credential, EventHubsOwnershipOptions ownershipOptions)
        => new(request => EventHubsSdkProcessor.ForCredential(fullyQualifiedNamespace, credential, request), ownershipOptions);

    public IEventHubsProcessor Create(string eventHubName, string consumerGroup, IEventHubsPartitionCallbacks callbacks)
        => createProcessor(new EventHubsProcessorRequest(
            eventHubName,
            consumerGroup,
            ownershipOptions.CreateStore(),
            ownershipOptions.ApplyTo(new EventProcessorOptions()),
            callbacks));
}
