using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Flowly.Transport;

namespace Flowly.AzureEventHubs;

/// <summary>
///     The non-partitioned counterpart of <see cref="EventHubsPartitionedStreamConsumer{TMessage}" />: one stock Event Hubs processor reading the single partition
///     of the event hub. The start position it is given is the one Flowly resolved, from a stored checkpoint position or the handler's configuration. When the SDK
///     reports the partition lost and initializes it again, reading resumes after the last event handed to the handler's loop instead of at that start position.
///     <para>
///         <see cref="StopProcessing" /> only stops dispatching, which is what a halted stream needs: the batch callback blocks and the SDK keeps the partition.
///         <see cref="DisposeAsync" /> stops the SDK processor and releases the partition.
///     </para>
/// </summary>
internal sealed class EventHubsStreamProcessor<TMessage>(
    IEventHubsProcessorFactory processorFactory,
    string eventHubName,
    string consumerGroup,
    StartPosition startPosition,
    bool sharesPartitionOwnership,
    TimeProvider timeProvider) : IMessageBusProcessor<TMessage>, IEventHubsPartitionCallbacks
{
    private const string OnlyPartitionId = "0";

    private readonly EventHubsPartitionProcessor<TMessage> _partitionProcessor = new(eventHubName);
    private readonly EventHubsErrorThrottle _errorThrottle = new(timeProvider, EventHubsErrorThrottle.DefaultInterval);
    private IEventHubsProcessor? _eventHubsProcessor;

    public event Func<IReceivedMessage<TMessage>, CancellationToken, Task> ProcessMessage
    {
        add => _partitionProcessor.ProcessMessage += value;
        remove => _partitionProcessor.ProcessMessage -= value;
    }

    public event Func<ErrorDetails, Task> ProcessError
    {
        add => _partitionProcessor.ProcessError += value;
        remove => _partitionProcessor.ProcessError -= value;
    }

    public async Task StartProcessingMessages(CancellationToken cancellationToken = default)
    {
        _eventHubsProcessor = processorFactory.Create(eventHubName, consumerGroup, this);

        await _eventHubsProcessor.StartProcessing(cancellationToken);
    }

    public Task StopProcessing(CancellationToken cancellationToken) => _partitionProcessor.StopProcessing(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _partitionProcessor.DisposeAsync();
        _partitionProcessor.Release();

        if (Interlocked.Exchange(ref _eventHubsProcessor, null) is { } eventHubsProcessor)
            await eventHubsProcessor.StopProcessing(CancellationToken.None);
    }

    public Task<EventPosition> GetStartingPosition(string partitionId, CancellationToken cancellationToken)
    {
        RequireOnlyPartition(partitionId);

        return Task.FromResult(
            _partitionProcessor.LastDispatchedSequenceNumber is { } lastDispatchedSequenceNumber
                ? EventHubsStartPositions.AfterSequenceNumber(lastDispatchedSequenceNumber)
                : EventHubsStartPositions.ToEventPosition(startPosition));
    }

    public Task InitializePartition(string partitionId, CancellationToken cancellationToken)
    {
        RequireOnlyPartition(partitionId);

        return Task.CompletedTask;
    }

    public Task StopPartition(string partitionId, ProcessingStoppedReason reason, CancellationToken cancellationToken)
    {
        if (reason != ProcessingStoppedReason.OwnershipLost)
        {
            _partitionProcessor.Release();

            return Task.CompletedTask;
        }

        return _partitionProcessor.RaiseError(new EventHubsOwnershipLostException(EventHubsOwnershipLoss.Describe(eventHubName, consumerGroup, partitionId, sharesPartitionOwnership)));
    }

    public Task ProcessEvents(string partitionId, IReadOnlyList<EventData> events, CancellationToken cancellationToken)
        => events.Count == 0 ? Task.CompletedTask : _partitionProcessor.Dispatch(events, cancellationToken);

    public Task HandleError(Exception exception, string? partitionId, string operation, CancellationToken cancellationToken)
        => partitionId is not null || _errorThrottle.TryReport(out _) ? _partitionProcessor.RaiseError(exception) : Task.CompletedTask;

    private void RequireOnlyPartition(string partitionId)
    {
        if (partitionId != OnlyPartitionId)
            throw new InvalidOperationException(
                $"Event hub '{eventHubName}' reported partition '{partitionId}', but the message contract is not partitioned, so Flowly reads exactly one partition with the id {OnlyPartitionId}. " +
                "Create the event hub with one partition, or declare [StreamPartitions] on the message contract.");
    }
}
