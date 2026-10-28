using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Flowly.Transport;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs;

/// <summary>
///     Adapts a single stock Event Hubs processor, which balances the partitions of the event hub across the instances that share its consumer group, onto
///     <see cref="IPartitionedStreamConsumer{TMessage}" />: a partition the SDK initializes becomes <see cref="PartitionAssigned" /> and a partition it reports as lost
///     becomes <see cref="PartitionRevoked" />. Flowly does not run an assignment protocol of its own.
///     <para>
///         The SDK asks for the start position of a newly initialized partition and initializes it, in an order this class does not depend on, so the start position is
///         resolved once per assignment and shared by both calls. It is resolved before the partition's processor exists, and a failure to resolve it is logged and thrown to
///         the SDK, so no processing loop is started for a partition whose position is unknown.
///     </para>
///     <para>
///         A halted partition (its retries are exhausted) keeps the batch callback of that partition blocked until the partition is lost or the host stops, so the SDK keeps
///         the ownership renewed, so the partition is not given away because its ownership expired. Rebalancing can still move it when another instance joins.
///     </para>
/// </summary>
internal sealed class EventHubsPartitionedStreamConsumer<TMessage>(
    IEventHubsProcessorFactory processorFactory,
    string eventHubName,
    string consumerGroup,
    int partitionCount,
    Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition,
    bool sharesPartitionOwnership,
    ILogger logger,
    TimeProvider timeProvider) : IPartitionedStreamConsumer<TMessage>, IEventHubsPartitionCallbacks
{
    private readonly Dictionary<int, EventHubsPartitionProcessor<TMessage>> _processors = new();
    private readonly Dictionary<int, PendingStartPosition> _pendingStartPositions = new();
    private readonly Lock _lock = new();
    private readonly EventHubsErrorThrottle _errorThrottle = new(timeProvider, EventHubsErrorThrottle.DefaultInterval);
    private IEventHubsProcessor? _eventHubsProcessor;

    public event Func<int, IMessageBusProcessor<TMessage>, Task>? PartitionAssigned;

    public event Func<int, Task>? PartitionRevoked;

    public async Task StartProcessingMessages(CancellationToken cancellationToken = default)
    {
        _eventHubsProcessor = processorFactory.Create(eventHubName, consumerGroup, this);

        await _eventHubsProcessor.StartProcessing(cancellationToken);
    }

    public Task StopProcessing(CancellationToken cancellationToken)
        => Interlocked.Exchange(ref _eventHubsProcessor, null)?.StopProcessing(cancellationToken) ?? Task.CompletedTask;

    public async ValueTask DisposeAsync() => await StopProcessing(CancellationToken.None);

    public async Task<EventPosition> GetStartingPosition(string partitionId, CancellationToken cancellationToken)
    {
        var partition = ToPartition(partitionId);

        return EventHubsStartPositions.ToEventPosition(await ResolveStartPositionOncePerAssignment(partition, AssignmentCall.GetStartingPosition, cancellationToken));
    }

    public async Task InitializePartition(string partitionId, CancellationToken cancellationToken)
    {
        var partition = ToPartition(partitionId);

        await ResolveStartPositionOncePerAssignment(partition, AssignmentCall.Initialize, cancellationToken);
        await RevokeProcessorBeingReplaced(partition);

        var processor = new EventHubsPartitionProcessor<TMessage>(eventHubName);
        lock (_lock)
        {
            _processors[partition] = processor;
        }

        if (PartitionAssigned is { } partitionAssigned)
            await partitionAssigned(partition, processor);
    }

    public async Task StopPartition(string partitionId, ProcessingStoppedReason reason, CancellationToken cancellationToken)
    {
        var partition = ToPartition(partitionId);
        EventHubsPartitionProcessor<TMessage>? processor;
        lock (_lock)
        {
            _processors.Remove(partition, out processor);
        }

        if (processor is null)
            return;

        if (reason != ProcessingStoppedReason.OwnershipLost)
        {
            processor.Release();

            return;
        }

        logger.Log(
            sharesPartitionOwnership ? LogLevel.Information : LogLevel.Warning,
            "{Message}",
            EventHubsOwnershipLoss.Describe(eventHubName, consumerGroup, partitionId, sharesPartitionOwnership));

        await FireRevoked(partition, processor);
    }

    public Task ProcessEvents(string partitionId, IReadOnlyList<EventData> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
            return Task.CompletedTask;

        EventHubsPartitionProcessor<TMessage>? processor;
        lock (_lock)
        {
            _processors.TryGetValue(ToPartition(partitionId), out processor);
        }

        if (processor is not null)
            return processor.Dispatch(events, cancellationToken);

        logger.LogWarning(
            "Received {Count} events on event hub '{EventHubName}' for partition {Partition} that this process is not currently processing. They are dropped without advancing any stored position.",
            events.Count,
            eventHubName,
            partitionId);

        return Task.CompletedTask;
    }

    public Task HandleError(Exception exception, string? partitionId, string operation, CancellationToken cancellationToken)
    {
        EventHubsPartitionProcessor<TMessage>? processor = null;
        if (partitionId is not null && EventHubsPartitionIds.ToPartition(partitionId) is { } partition)
        {
            lock (_lock)
            {
                _processors.TryGetValue(partition, out processor);
            }
        }

        if (processor is not null)
            return processor.RaiseError(exception);

        if (_errorThrottle.TryReport(out var suppressed))
            logger.LogError(exception, "Azure Event Hubs error on event hub '{EventHubName}' during '{Operation}' ({Suppressed} similar errors were not logged)", eventHubName, operation, suppressed);

        return Task.CompletedTask;
    }

    private int ToPartition(string partitionId)
        => EventHubsPartitionIds.ToPartition(partitionId) is { } partition && partition < partitionCount
            ? partition
            : throw new InvalidOperationException(
                $"Azure Event Hubs reported partition id '{partitionId}' for event hub '{eventHubName}', which is not one of the {partitionCount} partitions the message contract declares. " +
                $"Flowly maps partition id k to partition k and requires the partition ids of the event hub to be 0 to {partitionCount - 1}.");

    private Task<StartPosition> ResolveStartPositionOncePerAssignment(int partition, AssignmentCall call, CancellationToken cancellationToken)
    {
        Lazy<Task<StartPosition>> resolution;
        lock (_lock)
        {
            if (_pendingStartPositions.TryGetValue(partition, out var pending) && !pending.HasSeen(call))
            {
                pending.MarkSeen(call);
                resolution = pending.Resolution;

                if (pending.HasSeenAll)
                    _pendingStartPositions.Remove(partition);
            }
            else
            {
                var created = new PendingStartPosition(new Lazy<Task<StartPosition>>(() => ResolveAndLogFailure(partition, cancellationToken)));
                created.MarkSeen(call);
                _pendingStartPositions[partition] = created;
                resolution = created.Resolution;
            }
        }

        return resolution.Value;
    }

    private async Task<StartPosition> ResolveAndLogFailure(int partition, CancellationToken cancellationToken)
    {
        try
        {
            return await resolveStartPosition(partition, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Resolving the start position for partition {Partition} of event hub '{EventHubName}' failed. No processing loop is started for the partition until the SDK initializes it again.",
                partition,
                eventHubName);

            throw;
        }
    }

    private async Task RevokeProcessorBeingReplaced(int partition)
    {
        EventHubsPartitionProcessor<TMessage>? replaced;
        lock (_lock)
        {
            _processors.Remove(partition, out replaced);
        }

        if (replaced is null)
            return;

        logger.LogInformation(
            "Partition {Partition} of event hub '{EventHubName}' was initialized again without being reported lost first. " +
            "The previous processing loop for the partition is stopped before the new one starts.",
            partition,
            eventHubName);

        await FireRevoked(partition, replaced);
    }

    private async Task FireRevoked(int partition, EventHubsPartitionProcessor<TMessage> processor)
    {
        try
        {
            if (PartitionRevoked is { } partitionRevoked)
                await partitionRevoked(partition);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error while revoking partition {Partition} of event hub '{EventHubName}'", partition, eventHubName);
        }
        finally
        {
            processor.Release();
        }
    }

    private enum AssignmentCall
    {
        GetStartingPosition,
        Initialize
    }

    private sealed class PendingStartPosition(Lazy<Task<StartPosition>> resolution)
    {
        private bool _seenGetStartingPosition;
        private bool _seenInitialize;

        public Lazy<Task<StartPosition>> Resolution { get; } = resolution;

        public bool HasSeenAll => _seenGetStartingPosition && _seenInitialize;

        public bool HasSeen(AssignmentCall call) => call == AssignmentCall.Initialize ? _seenInitialize : _seenGetStartingPosition;

        public void MarkSeen(AssignmentCall call)
        {
            if (call == AssignmentCall.Initialize)
                _seenInitialize = true;
            else
                _seenGetStartingPosition = true;
        }
    }
}
