using System.Buffers;
using System.Text;
using System.Text.Json;
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
///         store is unreachable), the failure is logged and rethrown, and no processor or processing loop exists for
///         that partition afterwards, so <see cref="PartitionRevoked" /> has nothing to undo and no checkpoint is saved
///         for it. The Stream client does not propagate the exception and still subscribes the partition at its own
///         default offset (<c>next</c>), ignoring the configured or stored start position; the messages it then delivers
///         for the partition are dropped with a warning until the broker assigns the partition again. Other partitions
///         are unaffected.
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
internal sealed class RabbitMqPartitionedStreamConsumer<TMessage>(
    StreamSystem streamSystem,
    string queueName,
    Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition,
    ILogger logger) : IPartitionedStreamConsumer<TMessage>
{
    private readonly Dictionary<int, RabbitMqPartitionProcessor> _processors = new();
    private readonly Dictionary<int, Task> _pendingRevocations = new();
    private readonly Lock _lock = new();
    private Consumer? _consumer;

    public event Func<int, IMessageBusProcessor<TMessage>, Task>? PartitionAssigned;
    public event Func<int, Task>? PartitionRevoked;

    public async Task StartProcessingMessages(CancellationToken cancellationToken = default)
    {
        _consumer = await Consumer.Create(new ConsumerConfig(streamSystem, queueName)
        {
            IsSuperStream = true,
            IsSingleActiveConsumer = true,
            Reference = queueName,
            MessageHandler = async (sourceStream, _, messageContext, message) =>
            {
                var partition = ResolvePartitionIndex(sourceStream);
                var processor = GetProcessor(partition);
                if (processor is null)
                {
                    logger.LogWarning(
                        "Received a stream message on queue '{QueueName}' for partition {Partition} that this process is not " +
                        "currently processing — either a Single Active Consumer handover is in progress, or this partition was " +
                        "halted after exhausting its retries. The message is dropped without advancing any offset; it is only " +
                        "redelivered if a MessageStreamCheckpoint is registered for this consumer and its last saved position is " +
                        "at or before this message. Without a registered checkpoint, the next assignment resumes from the " +
                        "configured StartPosition instead.",
                        queueName, partition);
                    return;
                }

                await processor.Dispatch(message, (long)messageContext.Offset);
            },
            ConsumerUpdateListener = (_, sourceStream, isActive) => OnConsumerUpdate(sourceStream, isActive)
        });
    }

    internal async Task<IOffsetType> OnConsumerUpdate(string sourceStream, bool isActive)
    {
        var partition = ResolvePartitionIndex(sourceStream);

        if (!isActive)
        {
            RevokePartition(partition);
            return new OffsetTypeFirst();
        }

        await RevokeBeforeAssignment(partition);

        var startPosition = await ResolveStartPositionForAssignment(partition);

        var processor = new RabbitMqPartitionProcessor(this, logger, queueName, partition);
        lock (_lock)
        {
            _processors[partition] = processor;
        }

        if (PartitionAssigned != null)
            await PartitionAssigned(partition, processor);

        return startPosition.Match<IOffsetType>(
            first: () => new OffsetTypeFirst(),
            last: () => new OffsetTypeNext(),
            offset: n => new OffsetTypeOffset((ulong)n),
            timestamp: dt => new OffsetTypeTimestamp(dt));
    }

    public async Task StopProcessing(CancellationToken cancellationToken) => await CloseConsumer();

    public async ValueTask DisposeAsync() => await CloseConsumer();

    // DisposeAsync must independently close the consumer rather than relying on StopProcessing
    // having run first — IAsyncDisposable's contract requires DisposeAsync alone to release
    // resources. Interlocked.Exchange makes closing idempotent so StopProcessing followed by
    // DisposeAsync (or either alone) never double-closes the underlying broker subscription.
    private async Task CloseConsumer()
    {
        var consumer = Interlocked.Exchange(ref _consumer, null);
        if (consumer != null)
            await consumer.Close();
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

        logger.LogInformation(
            "Partition {Partition} of stream queue '{QueueName}' was assigned again without being revoked first, for example because the " +
            "stream connection was lost and recovered. The previous processing loop for the partition is stopped before the new one starts.",
            partition, queueName);

        await FireRevoked(partition);
    }

    // The start position is resolved before the processor is registered and PartitionAssigned is raised, so a
    // failure (for example an unreachable checkpoint store) leaves no processor and no processing loop behind for a
    // subscription that never received the resolved offset. The exception is logged here because the Stream client
    // swallows it, and rethrown so the client's own handling is unchanged.
    private async Task<StartPosition> ResolveStartPositionForAssignment(int partition)
    {
        try
        {
            return await resolveStartPosition(partition, CancellationToken.None);
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "Resolving the start position for partition {Partition} of stream queue '{QueueName}' failed. No processing loop is " +
                "started for the partition, so messages the stream client still delivers for it are dropped until it is assigned again.",
                partition, queueName);
            throw;
        }
    }

    private int ResolvePartitionIndex(string sourceStream)
    {
        var prefix = $"{queueName}-";
        if (!sourceStream.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(sourceStream[prefix.Length..], out var partition))
            throw new InvalidOperationException(
                $"Received a message from stream '{sourceStream}', which does not match the expected partition naming " +
                $"convention '{queueName}-<partition>' for Super Stream '{queueName}'.");

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
            logger.LogError(e, "Error while revoking partition {Partition} for stream queue '{QueueName}'", partition, queueName);
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
