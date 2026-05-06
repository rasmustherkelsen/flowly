namespace Flowly;

/// <summary>
///     Builder for registering a message handler. This is used to specify the queue name and provider name for the message
///     handler.
/// </summary>
/// <typeparam name="TMessage">Type of message that the handler will process</typeparam>
public interface IMessageHandlerBuilder<TMessage> : IFlowlyBuilder
{
}