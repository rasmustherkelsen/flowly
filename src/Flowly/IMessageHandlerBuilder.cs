using Flowly.MessageInfrastructure.Model;

namespace Flowly;

/// <summary>
///     Builder for registering a message handler. This is used to specify the queue name and provider name for the message
///     handler.
/// </summary>
/// <typeparam name="TMessage">Type of message that the handler will process</typeparam>
public interface IMessageHandlerBuilder<TMessage> : IFlowlyBuilder
{
    /// <summary>
    ///     Gets the handler settings for the message handler being registered. This includes information such as the queue
    ///     name, provider name, handler type, concurrency settings, and retry settings. This information is used internally by
    ///     the Flowly framework to configure the message processing pipeline for the handler.
    /// </summary>
    IHandlerSettings<TMessage> HandlerSettings { get; }
}