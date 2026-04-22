namespace Flowly;

/// <summary>
///     Use to send messages.
/// </summary>
public interface IMessageSender
{
    /// <summary>
    ///     Sends a message of type TMessage to the underlying messaging system.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TMessage"></typeparam>
    /// <returns></returns>
    Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default);
}