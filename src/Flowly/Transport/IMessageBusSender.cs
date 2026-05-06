namespace Flowly.Transport;

/// <summary>
///     Abstraction for sending messages to a single queue, topic, or exchange. Instances are created by
///     <see cref="IMessageBusClient.CreateMessageBusSender" /> (for queues) or
///     <see cref="IEventCapableMessageBusClient.CreateEventPublisher" /> (for topics). The framework obtains a sender
///     once and caches it for the lifetime of the application.
/// </summary>
public interface IMessageBusSender
{
    /// <summary>
    ///     Serialises <paramref name="message" /> to JSON and sends it with the supplied
    ///     <paramref name="messageProperties" /> as application-level metadata (correlation ID, session ID, scheduled
    ///     enqueue time, etc.). Called by the framework for normal message and event dispatch.
    /// </summary>
    /// <param name="message">The message payload to serialise and send.</param>
    /// <param name="messageProperties">Metadata to attach to the outgoing message.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <typeparam name="TMessage">The type of the message payload.</typeparam>
    Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sends a message with no body, carrying only the supplied <paramref name="messageProperties" />. Used internally
    ///     by the recurring job infrastructure to trigger job execution via a session-scoped lane queue.
    /// </summary>
    /// <param name="messageProperties">Metadata to attach to the outgoing message.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Re-publishes a message using its original raw body and application properties without re-serialising. Used by
    ///     the dead-letter requeue feature to resend a previously dead-lettered message exactly as it arrived, preserving
    ///     all broker-level metadata.
    /// </summary>
    /// <param name="rawBody">The original message body as a string (typically JSON).</param>
    /// <param name="applicationProperties">The original application properties to attach to the re-published message.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default);
}