namespace Flowly.Transport;

/// <summary>
///     Optional capability interface for transports that support append-only, replayable message streams (e.g.
///     RabbitMQ streams). Implemented alongside <see cref="IMessageBusClient" /> by clients whose broker offers a
///     stream primitive. Flowly checks for this interface eagerly at registration time — registering a
///     <see cref="MessageStreamHandler{TMessage}" /> or <see cref="IMessageRecorder" /> against a provider whose
///     client does not implement it throws <see cref="InvalidOperationException" /> at startup, not at first use.
/// </summary>
public interface IStreamCapableMessageBusClient
{
    /// <summary>
    ///     Creates a push-based processor consuming the given stream queue from <paramref name="startPosition" />.
    ///     The transport maps the start position onto its broker-specific consume argument (RabbitMQ:
    ///     <c>x-stream-offset</c>).
    /// </summary>
    /// <param name="queueName">The name of the stream queue to consume.</param>
    /// <param name="startPosition">The position in the stream from which consumption begins.</param>
    /// <param name="options">Processor options such as the maximum number of concurrent deliveries.</param>
    /// <typeparam name="TMessage">The expected message body type.</typeparam>
    /// <returns>A configured <see cref="IMessageBusProcessor{TMessage}" /> for the stream queue.</returns>
    Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options);
}
