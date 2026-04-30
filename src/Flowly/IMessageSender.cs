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
    /// <typeparam name="TMessage">The type of message to be sent.</typeparam>
    /// <returns>A task representing the asynchronous operation of sending the message.</returns>
    Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default);
}