using System.Buffers;
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
///         <strong>Best-effort against documented API shape, not verified against a live broker.</strong> The
///         exact semantics of <c>ConsumerUpdateListener</c>'s <c>isActive: false</c> case (whether it fires at all,
///         and whether the returned offset is ignored) are inferred from the client's public surface, not confirmed
///         by testing against a running RabbitMQ Super Stream. Validate before production use.
///     </para>
/// </summary>
internal sealed class RabbitMqPartitionedStreamConsumer<TMessage>(
    StreamSystem streamSystem,
    string queueName,
    Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition,
    ILogger logger) : IPartitionedStreamConsumer<TMessage>
{
    private readonly Dictionary<int, RabbitMqPartitionProcessor> _processors = new();
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
            ConsumerUpdateListener = async (consumerInstance, sourceStream, isActive) =>
            {
                var partition = ResolvePartitionIndex(sourceStream);

                if (!isActive)
                {
                    RemoveProcessor(partition);

                    if (PartitionRevoked != null)
                        _ = FireRevoked(partition);

                    return new OffsetTypeFirst();
                }

                var processor = new RabbitMqPartitionProcessor(this, logger, queueName, partition);
                lock (_lock)
                {
                    _processors[partition] = processor;
                }

                if (PartitionAssigned != null)
                    await PartitionAssigned(partition, processor);

                var startPosition = await resolveStartPosition(partition, CancellationToken.None);

                return startPosition.Match<IOffsetType>(
                    first: () => new OffsetTypeFirst(),
                    last: () => new OffsetTypeLast(),
                    offset: n => new OffsetTypeOffset((ulong)n),
                    timestamp: dt => new OffsetTypeTimestamp(dt));
            }
        });
    }

    public async Task StopProcessing(CancellationToken cancellationToken)
    {
        if (_consumer != null)
            await _consumer.Close();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

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
                _ => 0
            };
        }

        private static string? GetStringProperty(ApplicationProperties? properties, string key)
        {
            if (properties is null || !properties.TryGetValue(key, out var value)) return null;
            return value as string;
        }
    }
}
