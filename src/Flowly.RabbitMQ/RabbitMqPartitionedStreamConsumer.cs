using System.Buffers;
using System.Text;
using System.Text.Json;
using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.Transport;
using Microsoft.Extensions.Logging;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.AMQP;
using RabbitMQ.Stream.Client.Reliable;

namespace Flowly.RabbitMQ;

/// <summary>
///     Adapts a single RabbitMQ Stream protocol <see cref="Consumer" /> configured against a Super Stream
///     (<c>IsSuperStream = true</c>, <c>IsSingleActiveConsumer = true</c>) onto Flowly's
///     <see cref="IPartitionedStreamConsumer{TMessage}" /> contract — one <see cref="IMessageBusProcessor{TMessage}" />
///     per partition currently owned by this process. The underlying client delivers messages from every
///     currently-owned partition through a single callback tagged with the source partition stream name; this class
///     demultiplexes that into per-partition processors so Flowly never mixes partitions into one batch.
///     <para>
///         Partition ownership assignment and revocation are reported via <c>ConsumerUpdateListener</c> — the
///         broker notifies the client when it becomes (in)active for a given partition, which is exactly Flowly's
///         <see cref="PartitionAssigned" />/<see cref="PartitionRevoked" /> signal. The same callback is also where
///         the resume offset for a newly-active partition is resolved, via <c>resolveStartPosition</c> — this
///         naturally threads a registered <see cref="MessageStreamCheckpoint{TMessage}" /> into the broker's own
///         rebalancing protocol.
///     </para>
///     <para>
///         A reassignment of a given partition (a revoke followed by an assign, e.g. a brief Single Active Consumer
///         handover) waits for every earlier revocation of that same partition to finish running before the new
///         assignment proceeds, so the new <see cref="RabbitMqPartitionProcessor" /> is never created while the
///         previous processing loop for the partition is still draining. The <c>isActive: false</c> callback does not
///         wait for the revocation to finish, so a slow drain never stalls the broker's rebalance negotiation; the
///         wait happens in the next <c>isActive: true</c> callback for that same partition. This serialization is
///         scoped to a single partition: it never blocks assignment of other partitions.
///     </para>
///     <para>
///         The start position of a newly assigned partition is resolved before the partition's processor is registered
///         and <see cref="PartitionAssigned" /> is raised. When resolving it fails (for example because a checkpoint
///         store is unreachable), the callback keeps retrying with a growing delay inside a bounded window
///         (<see cref="RabbitMqPartitionedStreamTimings.ResolveWindow" />, kept under the 60 seconds after which the broker
///         closes the connection of a listener callback that has not returned). The connection of that partition delivers
///         nothing while the callback waits, so a short outage drops nothing, and with the default one consumer per connection
///         the other partitions are unaffected. When the partition is still unresolved at the end of the window, the failure is
///         logged, reported through <see cref="StartPositionResolutionExhausted" /> (the shared service records the partition as
///         waiting only then, not for an attempt that a later one absorbs) and rethrown, and no processor or processing loop exists for that partition afterwards, so
///         <see cref="PartitionRevoked" /> has nothing to undo and no checkpoint is saved for it. The Stream client does not
///         propagate the exception and still subscribes the partition at its own default offset (<c>next</c>), ignoring the
///         configured or stored start position; the messages it then delivers for the partition are dropped and counted
///         (a rate-limited summary in the log and the <c>flowly.message.stream.partition.dropped</c> counter) and replayed
///         later from the stored position. Other partitions are unaffected.
///     </para>
///     <para>
///         A partition that stays unresolved is retriggered by recreating the whole consumer
///         (<see cref="RabbitMqWaitingPartitionRecovery" />): the live processing loops are stopped (which also
///         releases the delivery a halted partition holds back), the consumer is closed and the consumer is created again, so the broker offers every partition to this process once more and the listener
///         resolves them anew, the healthy ones from their checkpoints (the in-flight batch of each is redelivered, and a halted partition replays its held batch and halts again). The delay
///         between two recreations grows up to a ceiling, only one recreation runs at a time, and the recovery ends when every
///         partition resolved or the consumer is stopped. Each consumer incarnation is a generation; once a generation is
///         replaced or closed its listener and message callbacks do nothing, so a late callback of the previous incarnation can
///         neither register a processing loop nor count as a drop.
///     </para>
///     <para>
///         The broker reports <c>isActive: false</c> to the instance that loses a partition to another instance and
///         ignores the offset returned for that call. It reports nothing to an instance whose connection was lost:
///         when the client recovers the subscription it reports <c>isActive: true</c> again without a preceding
///         <c>isActive: false</c>. A partition that is assigned while this process still holds a processing loop for
///         it is therefore treated as an implicit revocation first, and the assignment waits for that revocation as
///         well.
///     </para>
///     <para>
///         The client documents that it invokes <c>ConsumerUpdateListener</c> on the connection's socket thread and
///         that the broker closes the connection when the callback takes longer than 60 seconds. Because the wait for a
///         revocation happens inside that callback, a drain that runs longer than that would be affected. This
///         behaviour is taken from the client's documentation and has not been measured.
///     </para>
/// </summary>
internal sealed class RabbitMqPartitionedStreamConsumer<TMessage> : IPartitionedStreamConsumer<TMessage>, IStartPositionRetryingConsumer
{
    private readonly IRabbitMqSuperStreamConsumerFactory _consumerFactory;
    private readonly string _queueName;
    private readonly Func<int, CancellationToken, Task<StartPosition>> _resolveStartPosition;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly RabbitMqPartitionedStreamTimings _timings;
    private readonly RabbitMqStreamPartitionDropSummary _dropSummary;
    private readonly RabbitMqWaitingPartitionRecovery _recovery;
    private readonly Dictionary<int, RabbitMqPartitionProcessor> _processors = new();
    private readonly Dictionary<int, Task> _pendingRevocations = new();
    private readonly HashSet<int> _carriedWaitingPartitions = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Lock _lock = new();
    private Generation _generation = new();
    private IAsyncDisposable? _handle;
    private volatile bool _started;
    private volatile bool _closed;
    private bool _createFailed;
    private bool _settlePending;

    public RabbitMqPartitionedStreamConsumer(
        IRabbitMqSuperStreamConsumerFactory consumerFactory,
        string queueName,
        string consumerName,
        Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition,
        ILogger logger,
        TimeProvider? timeProvider = null,
        RabbitMqPartitionedStreamTimings? timings = null)
    {
        _consumerFactory = consumerFactory;
        _queueName = queueName;
        _resolveStartPosition = resolveStartPosition;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timings = timings ?? RabbitMqPartitionedStreamTimings.Default;
        _dropSummary = new RabbitMqStreamPartitionDropSummary(logger, _timeProvider, _timings.DropSummaryInterval, queueName, consumerName);
        _recovery = new RabbitMqWaitingPartitionRecovery(
            _timeProvider, logger, queueName, _timings.RecreationBackoffInitial, _timings.RecreationBackoffMax, NeedsRecreation, Recreate);
    }

    public event Func<int, IMessageBusProcessor<TMessage>, Task>? PartitionAssigned;
    public event Func<int, Task>? PartitionRevoked;
    public event Action<int, Exception>? StartPositionResolutionExhausted;

    public async Task StartProcessingMessages(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);

        try
        {
            _started = true;
            await CreateConsumer();
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task StopProcessing(CancellationToken cancellationToken) => await CloseConsumer();

    public async ValueTask DisposeAsync() => await CloseConsumer();

    internal Task RecoveryCompletion => _recovery.Completion;

    internal Task<IOffsetType> OnConsumerUpdate(string sourceStream, bool isActive) => OnConsumerUpdate(sourceStream, isActive, _generation);

    internal Task OnMessage(string sourceStream, long offset, Message message) => OnMessage(sourceStream, offset, message, _generation);

    private async Task CreateConsumer()
    {
        var generation = new Generation();
        _generation = generation;
        _handle = await _consumerFactory.Create(
            (sourceStream, offset, message) => OnMessage(sourceStream, offset, message, generation),
            (sourceStream, isActive) => OnConsumerUpdate(sourceStream, isActive, generation));
    }

    private async Task OnMessage(string sourceStream, long offset, Message message, Generation generation)
    {
        if (generation.IsCancelled) return;

        var partition = ResolvePartitionIndex(sourceStream);
        var processor = GetProcessor(partition);
        if (processor is null)
        {
            _dropSummary.Dropped(partition);
            return;
        }

        await processor.Dispatch(message, offset);
    }

    private async Task<IOffsetType> OnConsumerUpdate(string sourceStream, bool isActive, Generation generation)
    {
        var partition = ResolvePartitionIndex(sourceStream);

        if (!isActive)
        {
            generation.ClearFailed(partition);
            RevokePartition(partition);
            return new OffsetTypeFirst();
        }

        using var callback = generation.TrackCallback();
        using var window = new CancellationTokenSource(_timings.ResolveWindow, _timeProvider);
        using var resolution = CancellationTokenSource.CreateLinkedTokenSource(generation.Token, window.Token);

        await RevokeBeforeAssignment(partition);

        StartPosition startPosition;
        try
        {
            startPosition = await ResolveStartPositionForAssignment(partition, generation, resolution.Token);
        }
        catch (Exception exception) when (!generation.IsCancelled)
        {
            generation.MarkFailed(partition);
            StartPositionResolutionExhausted?.Invoke(partition, exception);
            if (_started) _recovery.Trigger();
            throw;
        }

        var processor = new RabbitMqPartitionProcessor(this, _logger, _queueName, partition);
        lock (_lock)
        {
            generation.Token.ThrowIfCancellationRequested();
            _processors[partition] = processor;
        }

        generation.ClearFailed(partition);
        _dropSummary.Started(partition);

        if (PartitionAssigned != null)
            await PartitionAssigned(partition, processor);

        return startPosition.Match<IOffsetType>(
            first: () => new OffsetTypeFirst(),
            last: () => new OffsetTypeNext(),
            offset: n => new OffsetTypeOffset((ulong)n),
            timestamp: dt => new OffsetTypeTimestamp(dt));
    }

    // Closing is idempotent: StopProcessing followed by DisposeAsync (or either alone) never double-closes the
    // underlying broker subscription, and DisposeAsync alone releases it, as IAsyncDisposable requires. The generation
    // is cancelled first so the callbacks of the closing consumer do nothing, and the recovery is stopped before the
    // consumer is closed so a recreation cannot start a new one behind it.
    private async Task CloseConsumer()
    {
        _closed = true;
        _generation.Cancel();
        await _recovery.Stop();

        await _lifecycle.WaitAsync();

        try
        {
            var handle = Interlocked.Exchange(ref _handle, null);

            if (handle != null)
                await handle.DisposeAsync();

            await _generation.WhenIdle();
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    // Stops the processing loops, closes the consumer and creates the consumer again, so the broker offers every
    // partition to this process once more and the listener resolves their start positions anew. The partitions that were
    // waiting are remembered so a partition that no longer comes back (it moved to another instance) can be released
    // afterwards. The generation is cancelled first, so the callbacks of the old consumer ignore what it still delivers while the loops are stopped, and
    // the loops are stopped before the consumer is closed: a halted partition holds its delivery back (backpressure), and stopping its held runner is
    // what releases that delivery, so closing the consumer first could wait for a delivery that only the stopped loop can release.
    private async Task Recreate(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken);

        try
        {
            if (_closed) return;

            var previous = _generation;
            var previousHandle = Interlocked.Exchange(ref _handle, null);

            lock (_lock)
            {
                _carriedWaitingPartitions.UnionWith(previous.FailedPartitions());
            }

            previous.Cancel();
            _logger.LogInformation("Recreating the partitioned consumer of stream queue '{QueueName}': stopping the processing loops and closing it.", _queueName);

            await previous.WhenIdle();
            await RevokeAllLiveProcessors();

            if (previousHandle != null)
                await previousHandle.DisposeAsync();

            try
            {
                await CreateConsumer();
                _createFailed = false;
                _settlePending = true;
                _logger.LogInformation("The partitioned consumer of stream queue '{QueueName}' was recreated.", _queueName);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _createFailed = true;
                _logger.LogError(exception, "Creating the partitioned consumer of stream queue '{QueueName}' failed; it is tried again.", _queueName);
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    // Whether another recreation is needed. After a recreation the broker is given a moment to report the partitions again
    // before the answer is read; the callbacks still in flight (each bounded by the resolution window) are awaited. The
    // partitions that were waiting before the recreation and were not reported again have moved to another instance, so
    // their waiting state is released.
    private async Task<bool> NeedsRecreation(CancellationToken cancellationToken)
    {
        if (_settlePending)
        {
            await Task.Delay(_timings.RecreationSettleDelay, _timeProvider, cancellationToken);
            _settlePending = false;
        }

        var generation = _generation;
        await generation.WhenIdle().WaitAsync(cancellationToken);

        if (_createFailed) return true;

        await ReleaseWaitingPartitionsThatDidNotReturn(generation);

        return generation.HasFailedPartitions;
    }

    private async Task ReleaseWaitingPartitionsThatDidNotReturn(Generation generation)
    {
        int[] gone;
        lock (_lock)
        {
            gone = _carriedWaitingPartitions.Where(partition => !generation.HasFailed(partition) && !_processors.ContainsKey(partition)).ToArray();
            _carriedWaitingPartitions.Clear();
        }

        if (PartitionRevoked == null) return;

        await Task.WhenAll(gone.Select(FireRevoked));
    }

    private async Task RevokeAllLiveProcessors()
    {
        int[] partitions;
        Task[] pending;
        lock (_lock)
        {
            partitions = [.._processors.Keys];
            _processors.Clear();
            pending = [.._pendingRevocations.Values];
            _pendingRevocations.Clear();
        }

        await Task.WhenAll(pending);

        if (PartitionRevoked == null) return;

        await Task.WhenAll(partitions.Select(FireRevoked));
    }

    private RabbitMqPartitionProcessor? GetProcessor(int partition)
    {
        lock (_lock)
        {
            return _processors.GetValueOrDefault(partition);
        }
    }

    private void RemoveProcessor(int partition)
    {
        lock (_lock)
        {
            _processors.Remove(partition);
        }
    }

    private void RevokePartition(int partition)
    {
        RemoveProcessor(partition);

        if (PartitionRevoked == null) return;

        var revokeTask = FireRevoked(partition);
        lock (_lock)
        {
            _pendingRevocations[partition] = _pendingRevocations.TryGetValue(partition, out var previousRevocation)
                ? Task.WhenAll(previousRevocation, revokeTask)
                : revokeTask;
        }
    }

    // The broker reports isActive: false only when another instance takes the partition over. When this
    // process loses its connection and the client recovers the subscription, isActive: true arrives without a
    // preceding isActive: false, so a live processor for the partition is revoked here before the new one starts.
    private async Task RevokeBeforeAssignment(int partition)
    {
        Task? pendingRevocation;
        bool replacesLiveProcessor;
        lock (_lock)
        {
            _pendingRevocations.Remove(partition, out pendingRevocation);
            replacesLiveProcessor = _processors.Remove(partition);
        }

        if (pendingRevocation != null)
            await pendingRevocation;

        if (!replacesLiveProcessor || PartitionRevoked == null) return;

        _logger.LogInformation(
            "Partition {Partition} of stream queue '{QueueName}' was assigned again without being revoked first, for example because the " +
            "stream connection was lost and recovered. The previous processing loop for the partition is stopped before the new one starts.",
            partition, _queueName);

        await FireRevoked(partition);
    }

    // The start position is resolved before the processor is registered and PartitionAssigned is raised, so a failure
    // (for example an unreachable checkpoint store) leaves no processor and no processing loop behind for a subscription that
    // never received the resolved offset. A failed attempt is retried with a growing delay until the window ends, which keeps
    // the callback under the broker's 60 second limit; the partition's connection delivers nothing while it waits. When the
    // window ends the last failure is logged and rethrown, because the Stream client swallows it.
    private async Task<StartPosition> ResolveStartPositionForAssignment(int partition, Generation generation, CancellationToken windowToken)
    {
        var backoff = _timings.ResolveBackoffInitial;
        Exception? lastFailure = null;

        for (var attempt = 1;; attempt++)
        {
            try
            {
                return await _resolveStartPosition(partition, generation.Token).WaitAsync(windowToken);
            }
            catch (Exception exception)
            {
                if (generation.IsCancelled) throw new OperationCanceledException(generation.Token);

                if (exception is not OperationCanceledException || !windowToken.IsCancellationRequested)
                {
                    lastFailure = exception;
                    _dropSummary.ResolutionFailed(partition, exception);
                    _logger.LogWarning(exception,
                        "Resolving the start position for partition {Partition} of stream queue '{QueueName}' failed (attempt {Attempt}).",
                        partition, _queueName, attempt);
                }
            }

            if (windowToken.IsCancellationRequested) break;

            try
            {
                await Task.Delay(backoff, _timeProvider, windowToken);
            }
            catch (OperationCanceledException)
            {
                if (generation.IsCancelled) throw;
                break;
            }

            backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, _timings.ResolveBackoffMax.Ticks));
        }

        var failure = lastFailure ?? new TimeoutException(
            $"Resolving the start position for partition {partition} of stream queue '{_queueName}' did not finish within {_timings.ResolveWindow.TotalSeconds:N0} s.");

        _logger.LogError(failure,
            "Resolving the start position for partition {Partition} of stream queue '{QueueName}' still failed after {Seconds:N0} s. No processing loop is " +
            "started for the partition and the messages the stream client still delivers for it are dropped; the partition is waiting, and the consumer is " +
            "recreated with a growing delay until it resolves.",
            partition, _queueName, _timings.ResolveWindow.TotalSeconds);

        throw failure;
    }

    private int ResolvePartitionIndex(string sourceStream)
    {
        var prefix = $"{_queueName}-";
        if (!sourceStream.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(sourceStream[prefix.Length..], out var partition))
            throw new InvalidOperationException(
                $"Received a message from stream '{sourceStream}', which does not match the expected partition naming " +
                $"convention '{_queueName}-<partition>' for Super Stream '{_queueName}'.");

        return partition;
    }

    // isActive: false is not awaited synchronously inside ConsumerUpdateListener so a slow handler/retry drain
    // never blocks the Stream client's own rebalance negotiation; failures during the drain are logged here
    // instead of propagating to an unobserved task.
    private async Task FireRevoked(int partition)
    {
        try
        {
            await PartitionRevoked!(partition);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while revoking partition {Partition} for stream queue '{QueueName}'", partition, _queueName);
        }
    }

    private sealed class Generation
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly HashSet<int> _failed = new();
        private readonly List<Task> _callbacks = new();
        private readonly Lock _lock = new();

        public CancellationToken Token => _cancellation.Token;

        public bool IsCancelled => _cancellation.IsCancellationRequested;

        public bool HasFailedPartitions
        {
            get
            {
                lock (_lock)
                {
                    return _failed.Count > 0;
                }
            }
        }

        public void Cancel() => _cancellation.Cancel();

        public bool HasFailed(int partition)
        {
            lock (_lock)
            {
                return _failed.Contains(partition);
            }
        }

        public int[] FailedPartitions()
        {
            lock (_lock)
            {
                return [.._failed];
            }
        }

        public void MarkFailed(int partition)
        {
            lock (_lock)
            {
                _failed.Add(partition);
            }
        }

        public void ClearFailed(int partition)
        {
            lock (_lock)
            {
                _failed.Remove(partition);
            }
        }

        public IDisposable TrackCallback()
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
            {
                _callbacks.RemoveAll(callback => callback.IsCompleted);
                _callbacks.Add(completion.Task);
            }

            return new CallbackScope(completion);
        }

        public Task WhenIdle()
        {
            lock (_lock)
            {
                return Task.WhenAll(_callbacks);
            }
        }

        private sealed class CallbackScope(TaskCompletionSource completion) : IDisposable
        {
            public void Dispose() => completion.TrySetResult();
        }
    }

    private sealed class RabbitMqPartitionProcessor(
        RabbitMqPartitionedStreamConsumer<TMessage> owner,
        ILogger logger,
        string queueName,
        int partition) : IMessageBusProcessor<TMessage>
    {
        private readonly List<Func<IReceivedMessage<TMessage>, CancellationToken, Task>> _messageHandlers = [];
        private readonly List<Func<ErrorDetails, Task>> _errorHandlers = [];
        private readonly Lock _handlersLock = new();

        public event Func<IReceivedMessage<TMessage>, CancellationToken, Task> ProcessMessage
        {
            add
            {
                lock (_handlersLock)
                {
                    _messageHandlers.Add(value);
                }
            }
            remove
            {
                lock (_handlersLock)
                {
                    _messageHandlers.Remove(value);
                }
            }
        }

        public event Func<ErrorDetails, Task> ProcessError
        {
            add
            {
                lock (_handlersLock)
                {
                    _errorHandlers.Add(value);
                }
            }
            remove
            {
                lock (_handlersLock)
                {
                    _errorHandlers.Remove(value);
                }
            }
        }

        public Task StartProcessingMessages(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopProcessing(CancellationToken cancellationToken)
        {
            owner.RemoveProcessor(partition);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            owner.RemoveProcessor(partition);
            return ValueTask.CompletedTask;
        }

        public async Task Dispatch(Message message, long offset)
        {
            List<Func<IReceivedMessage<TMessage>, CancellationToken, Task>> handlers;
            lock (_handlersLock)
            {
                handlers = [.._messageHandlers];
            }

            try
            {
                var received = new RabbitMqPartitionedReceivedMessage(message, offset);

                foreach (var handler in handlers)
                    await handler(received, CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error dispatching a stream message on queue '{QueueName}' partition {Partition}", queueName, partition);

                List<Func<ErrorDetails, Task>> errorHandlers;
                lock (_handlersLock)
                {
                    errorHandlers = [.._errorHandlers];
                }

                var error = new ErrorDetails(e, queueName);
                foreach (var errorHandler in errorHandlers)
                    await errorHandler(error);
            }
        }
    }

    private sealed class RabbitMqPartitionedReceivedMessage(Message message, long offset) : IReceivedMessage<TMessage>
    {
        private readonly byte[] _body = message.Data.Contents.ToArray();

        public TMessage Body => field ??= JsonSerializer.Deserialize<TMessage>(_body)
            ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

        public MessageProperties Properties { get; } = new(
            message.Properties?.MessageId?.ToString() ?? Guid.NewGuid().ToString(),
            message.Properties?.CorrelationId?.ToString() ?? string.Empty,
            RetryCount: GetRetryCount(message.ApplicationProperties),
            Traceparent: GetStringProperty(message.ApplicationProperties, "traceparent"),
            Tracestate: GetStringProperty(message.ApplicationProperties, "tracestate"),
            StreamOffset: offset);

        public Task Complete(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "Stream messages do not support dead-lettering. A batch that exhausts its retries halts consumption of that partition instead.");

        private static int GetRetryCount(ApplicationProperties? properties)
        {
            if (properties is null || !properties.TryGetValue(FlowlyMessageProperties.RetryCount, out var value)) return 0;
            return value switch
            {
                int i => i,
                long l => (int)l,
                byte[] b => int.TryParse(Encoding.UTF8.GetString(b), out var parsed) ? parsed : 0,
                _ => 0
            };
        }

        private static string? GetStringProperty(ApplicationProperties? properties, string key)
        {
            if (properties is null || !properties.TryGetValue(key, out var value)) return null;
            return value switch
            {
                string s => s,
                byte[] b => Encoding.UTF8.GetString(b),
                _ => null
            };
        }
    }
}
