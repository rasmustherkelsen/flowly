using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeEventHubsProcessor(string eventHubName, string consumerGroup, IEventHubsPartitionCallbacks callbacks) : IEventHubsProcessor
{
    public string EventHubName => eventHubName;

    public string ConsumerGroup => consumerGroup;

    public IEventHubsPartitionCallbacks Callbacks => callbacks;

    public Exception? StartFailure { get; set; }

    public int Starts { get; private set; }

    public int Stops { get; private set; }

    public Task StartProcessing(CancellationToken cancellationToken)
    {
        Starts++;

        return StartFailure is null ? Task.CompletedTask : Task.FromException(StartFailure);
    }

    public Task StopProcessing(CancellationToken cancellationToken)
    {
        Stops++;

        return Task.CompletedTask;
    }

    public async Task<EventPosition> Assign(string partitionId)
    {
        var position = await callbacks.GetStartingPosition(partitionId, CancellationToken.None);
        await callbacks.InitializePartition(partitionId, CancellationToken.None);

        return position;
    }

    public async Task<EventPosition> AssignInitializingFirst(string partitionId)
    {
        await callbacks.InitializePartition(partitionId, CancellationToken.None);

        return await callbacks.GetStartingPosition(partitionId, CancellationToken.None);
    }

    public Task Lose(string partitionId) => callbacks.StopPartition(partitionId, ProcessingStoppedReason.OwnershipLost, CancellationToken.None);

    public Task Shutdown(string partitionId) => callbacks.StopPartition(partitionId, ProcessingStoppedReason.Shutdown, CancellationToken.None);

    public Task Deliver(string partitionId, params EventData[] events) => callbacks.ProcessEvents(partitionId, events, CancellationToken.None);

    public Task Fail(Exception exception, string? partitionId) => callbacks.HandleError(exception, partitionId, "operation", CancellationToken.None);
}
