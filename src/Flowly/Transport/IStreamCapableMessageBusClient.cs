namespace Flowly.Transport;

/// <summary>
///     Stream capability of a transport provider: append-only, replayable message streams (e.g. RabbitMQ streams). A client
///     that also implements <see cref="IMessageBusClient" /> offers streams alongside queues; a client that implements only
///     this interface is a stream-only transport and cannot host queue-based handlers or submitters. Flowly checks for this
///     interface eagerly at registration time — registering a <see cref="MessageStreamHandler{TMessage}" /> or
///     <see cref="IMessageRecorder" /> against a provider whose client does not implement it throws
///     <see cref="InvalidOperationException" /> at startup, not at first use. A client that can also translate a
///     <see cref="StartPosition" /> into a concrete offset without consuming implements <see cref="IStreamStartOffsetResolver" />
///     in addition, which lets a registered <see cref="MessageStreamCheckpoint{TMessage}" /> fix the first run's position up front.
/// </summary>
public interface IStreamCapableMessageBusClient : ITransportClient
{
    /// <summary>
    ///     Creates a push-based processor consuming the given stream queue from <paramref name="startPosition" />.
    ///     The transport maps the start position onto its broker-specific consume argument (RabbitMQ:
    ///     <c>x-stream-offset</c>). An implementation must translate <see cref="StartPosition.Last" /> so that only messages
    ///     appended after the processor attaches are delivered, never messages already in the stream (RabbitMQ: <c>next</c>,
    ///     not <c>last</c>).
    /// </summary>
    /// <param name="queueName">The name of the stream queue to consume.</param>
    /// <param name="startPosition">The position in the stream from which consumption begins.</param>
    /// <param name="options">Processor options such as the maximum number of concurrent deliveries.</param>
    /// <typeparam name="TMessage">The expected message body type.</typeparam>
    /// <returns>A configured <see cref="IMessageBusProcessor{TMessage}" /> for the stream queue.</returns>
    Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options);

    /// <summary>
    ///     Creates a sender that appends messages to the given stream queue. The framework calls this method for every
    ///     <see cref="IMessageRecorder" /> registration. The sender honours <see cref="MessageProperties.PartitionKey" /> when
    ///     the stream is partitioned. The default implementation delegates to
    ///     <see cref="IMessageBusClient.CreateMessageBusSender" /> when the client also implements
    ///     <see cref="IMessageBusClient" />, so queue-and-stream transports need no change; a stream-only transport must
    ///     override it.
    /// </summary>
    /// <param name="queueName">The name of the stream queue to append to.</param>
    /// <returns>An <see cref="IMessageBusSender" /> targeting the stream queue.</returns>
    /// <exception cref="NotSupportedException">
    ///     Surfaced through the returned task when the client does not implement <see cref="IMessageBusClient" /> and has not
    ///     overridden this method.
    /// </exception>
    Task<IMessageBusSender> CreateStreamSender(string queueName)
        => this is IMessageBusClient messageBusClient
            ? messageBusClient.CreateMessageBusSender(queueName)
            : Task.FromException<IMessageBusSender>(
                new NotSupportedException($"{GetType().Name} is a stream-only transport client and must override {nameof(CreateStreamSender)}."));
}
