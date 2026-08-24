namespace Flowly;

/// <summary>
///     Use to record messages onto an append-only, replayable message stream. The third sending verb alongside
///     <see cref="IMessageSender" /> (fire-and-forget) and <see cref="IMessageCaller" /> (RPC-style call). Requires
///     <c>AddMessageRecorder&lt;TMessage&gt;()</c> registration and a transport whose client implements
///     <see cref="Transport.IStreamCapableMessageBusClient" /> (currently RabbitMQ or InMemory) — registration throws
///     <see cref="InvalidOperationException" /> at startup on any other transport.
/// </summary>
public interface IMessageRecorder
{
    /// <summary>
    ///     Records a message of type TMessage onto its stream. Recorded messages remain in the stream until evicted by
    ///     the retention limits configured via <see cref="StreamRetentionAttribute" /> and can be replayed by any
    ///     <see cref="MessageStreamHandler{TMessage}" /> — including consumers registered after the message was
    ///     recorded.
    /// </summary>
    /// <param name="message">The message to record.</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used to signal when the record operation should be cancelled.
    /// </param>
    /// <param name="partitionKey">
    ///     For a stream partitioned via <see cref="StreamPartitionsAttribute" />, selects which partition this
    ///     message is routed to (via a transport-specific hash) — messages recorded with the same key always land
    ///     in the same partition, giving them a relative ordering guarantee they would not otherwise have.
    ///     <see langword="null" /> (the default) selects a partition via round-robin, with no ordering guarantee
    ///     relative to other messages. Ignored for a non-partitioned stream. Placed after
    ///     <paramref name="cancellationToken" />, not before it, so existing positional
    ///     <c>Record(message, cancellationToken)</c> call sites keep compiling unchanged.
    /// </param>
    /// <typeparam name="TMessage">The type of message to be recorded.</typeparam>
    /// <returns>A task representing the asynchronous operation of recording the message.</returns>
    Task Record<TMessage>(TMessage message, CancellationToken cancellationToken = default, string? partitionKey = null);
}
