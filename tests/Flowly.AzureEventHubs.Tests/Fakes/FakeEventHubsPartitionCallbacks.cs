using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeEventHubsPartitionCallbacks : IEventHubsPartitionCallbacks
{
    public List<string> OwnedByAnotherInstance { get; } = [];

    public Task<EventPosition> GetStartingPosition(string partitionId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task InitializePartition(string partitionId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task StopPartition(string partitionId, ProcessingStoppedReason reason, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ProcessEvents(string partitionId, IReadOnlyList<EventData> events, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task HandleError(Exception exception, string? partitionId, string operation, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task PartitionOwnedByAnotherInstance(string partitionId, CancellationToken cancellationToken)
    {
        OwnedByAnotherInstance.Add(partitionId);

        return Task.CompletedTask;
    }
}
