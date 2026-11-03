using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;

namespace Flowly.AzureEventHubs;

internal interface IEventHubsPartitionCallbacks
{
    Task<EventPosition> GetStartingPosition(string partitionId, CancellationToken cancellationToken);

    Task InitializePartition(string partitionId, CancellationToken cancellationToken);

    Task StopPartition(string partitionId, ProcessingStoppedReason reason, CancellationToken cancellationToken);

    Task ProcessEvents(string partitionId, IReadOnlyList<EventData> events, CancellationToken cancellationToken);

    Task HandleError(Exception exception, string? partitionId, string operation, CancellationToken cancellationToken);

    Task PartitionOwnedByAnotherInstance(string partitionId, CancellationToken cancellationToken) => Task.CompletedTask;
}
