namespace Flowly;

/// <summary>
///     Base class for handlers that consume an append-only, replayable message stream. Register with
///     <c>AddMessageStreamHandler&lt;TMessage, THandler&gt;()</c>. Only supported on transports whose client implements
///     <see cref="Transport.IStreamCapableMessageBusClient" /> (currently RabbitMQ) — registration throws
///     <see cref="InvalidOperationException" /> at startup on any other transport.
///     <para>
///         Messages are delivered in batches shaped by <see cref="MessageStreamHandlerOptions.MaxMessagesBeforeProcessing" />
///         and <see cref="MessageStreamHandlerOptions.MaxWaitTime" />. Consumption begins at the
///         <see cref="MessageStreamHandlerOptions.StartPosition" /> configured in <see cref="Configure" /> — a required,
///         explicit choice that is re-evaluated on every process restart (offsets are not persisted).
///     </para>
///     <para>
///         Retry is opt-in via <see cref="RetryPolicyAttribute" /> on the handler class and runs as an in-process loop
///         over the same batch — failed messages are never re-published to the stream, which would permanently corrupt
///         the replayable log. When retries are exhausted the handler <strong>halts consumption entirely</strong>
///         rather than skipping the failed batch: the stream offset never advances past it, a critical log entry and a
///         <c>flowly.message.handler.halted</c> metric are emitted, and manual intervention (fix and restart) is
///         required. Dead letter tracking is not supported.
///     </para>
/// </summary>
/// <typeparam name="TMessage">The message contract type consumed from the stream.</typeparam>
public abstract class MessageStreamHandler<TMessage>
{
    /// <summary>
    ///     Configures the stream handler. Override to set the required
    ///     <see cref="MessageStreamHandlerOptions.StartPosition" /> and any batching, concurrency, or retention
    ///     settings. Values set here take precedence over attribute-based configuration on the handler class and
    ///     message contract.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    public virtual void Configure(MessageStreamHandlerOptions options)
    {
    }

    /// <summary>
    ///     Handles a batch of messages read from the stream. Throwing triggers the in-process retry behavior described
    ///     on the class documentation.
    /// </summary>
    /// <param name="messageContext">The context containing the batch of messages and a cancellation token.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public abstract Task Handle(IMessageStreamContext<TMessage> messageContext);
}
