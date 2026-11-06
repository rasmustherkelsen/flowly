namespace Flowly;

/// <summary>
///     Use to send messages.
/// </summary>
public interface IMessageSender
{
    /// <summary>
    ///     Sends a message of type TMessage to the underlying messaging system.
    /// </summary>
    /// <param name="message">The message to be sent.</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used to signal when the send operation should be
    ///     cancelled.
    /// </param>
    /// <param name="customProperties">
    ///     Optional application-defined string key/value pairs delivered with the message and readable by the receiver in
    ///     <see cref="MessageProperties.CustomProperties" />. Keys starting with <c>flowly-</c> and the keys
    ///     <c>traceparent</c> and <c>tracestate</c> are reserved. The values are not authenticated.
    /// </param>
    /// <typeparam name="TMessage">The type of message to be sent.</typeparam>
    /// <returns>A task representing the asynchronous operation of sending the message.</returns>
    /// <exception cref="ArgumentException"><paramref name="customProperties" /> contains an empty or reserved key or a null value.</exception>
    Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string>? customProperties = null);
}