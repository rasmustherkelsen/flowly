using Microsoft.Extensions.Logging;

namespace Flowly.Transport;

/// <summary>
///     Optional capability interface for transports that support partitioned, append-only, replayable message
///     streams (e.g. RabbitMQ Super Streams). Implemented alongside <see cref="IMessageBusClient" /> and, typically,
///     <see cref="IStreamCapableMessageBusClient" /> by clients whose broker offers a partitioned-stream primitive.
///     Flowly checks for this interface eagerly at registration time — registering a
///     <see cref="MessageStreamHandler{TMessage}" /> or <see cref="IMessageRecorder" /> for a message contract
///     carrying <see cref="StreamPartitionsAttribute" /> against a provider whose client does not implement it
///     throws <see cref="InvalidOperationException" /> at startup, not at first use.
///     <para>
///         Flowly does not implement its own cross-instance partition-ownership or rebalancing protocol — that
///         responsibility belongs entirely to the transport, using whatever native mechanism it has (e.g. RabbitMQ's
///         broker-coordinated Single Active Consumer). Flowly's role is limited to running one independent
///         processing loop per partition currently owned by this process, reacting to ownership changes as
///         <see cref="IPartitionedStreamConsumer{TMessage}" /> reports them.
///     </para>
/// </summary>
public interface IPartitionedStreamCapableMessageBusClient
{
    /// <summary>
    ///     Creates a consumer for a partitioned stream queue. The transport owns partition assignment and
    ///     rebalancing internally; Flowly reacts to <see cref="IPartitionedStreamConsumer{TMessage}.PartitionAssigned" />
    ///     and <see cref="IPartitionedStreamConsumer{TMessage}.PartitionRevoked" /> as they're raised.
    /// </summary>
    /// <param name="queueName">The name of the partitioned stream queue to consume.</param>
    /// <param name="partitionCount">The number of partitions, from <see cref="StreamPartitionsAttribute" /> on the message contract.</param>
    /// <param name="resolveStartPosition">
    ///     Called by the transport when it needs to know where consumption should begin for a given partition index
    ///     — once when this process first becomes responsible for that partition. Wired by Flowly to consult a
    ///     registered <see cref="MessageStreamCheckpoint{TMessage}" /> if present, falling back to the configured
    ///     <see cref="MessageStreamHandlerOptions.StartPosition" /> otherwise — the same precedence as the
    ///     non-partitioned case, evaluated per partition.
    /// </param>
    /// <param name="options">Processor options such as the maximum number of messages to accumulate before invoking the handler.</param>
    /// <param name="logger">
    ///     A logger scoped to the owning background service, used by transports whose delegation model can
    ///     transiently receive a message for a partition this process no longer (or does not yet) own — e.g. during
    ///     a broker-coordinated rebalance handover — so that condition is observable instead of silently dropped.
    /// </param>
    /// <typeparam name="TMessage">The expected message body type.</typeparam>
    /// <returns>A configured <see cref="IPartitionedStreamConsumer{TMessage}" />.</returns>
    Task<IPartitionedStreamConsumer<TMessage>> CreatePartitionedStreamConsumer<TMessage>(
        string queueName,
        int partitionCount,
        Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition,
        MessageBusProcessorOptions options,
        ILogger logger);
}

/// <summary>
///     Consumes a partitioned stream, reporting which partitions this process currently owns as the transport's
///     native rebalancing mechanism assigns and revokes them. Each owned partition gets its own
///     <see cref="IMessageBusProcessor{TMessage}" />, functionally identical to the one a non-partitioned
///     <see cref="IStreamCapableMessageBusClient.CreateStreamProcessor{TMessage}" /> would return — Flowly runs the
///     same accumulate-batch-and-process loop against it, once per owned partition, never mixing messages from
///     different partitions into the same batch (per-partition ordering only holds within a partition).
/// </summary>
/// <typeparam name="TMessage">The expected message body type.</typeparam>
public interface IPartitionedStreamConsumer<TMessage> : IAsyncDisposable
{
    /// <summary>
    ///     Raised when this process becomes responsible for a partition — either at startup or after a rebalance.
    ///     The supplied <see cref="IMessageBusProcessor{TMessage}" /> delivers only that partition's messages.
    /// </summary>
    event Func<int, IMessageBusProcessor<TMessage>, Task> PartitionAssigned;

    /// <summary>
    ///     Raised when this process is no longer responsible for a partition — after a rebalance moves it elsewhere.
    ///     Flowly stops and disposes that partition's processing loop; no further <c>ProcessMessage</c> events for
    ///     it should be raised on the processor supplied by the corresponding <see cref="PartitionAssigned" />.
    /// </summary>
    event Func<int, Task> PartitionRevoked;

    /// <summary>
    ///     Begins consuming. Called once by the background service during application start.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to stop consuming.</param>
    Task StartProcessingMessages(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gracefully stops consuming across all currently owned partitions.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that limits the time allowed for a graceful stop.</param>
    Task StopProcessing(CancellationToken cancellationToken);
}
