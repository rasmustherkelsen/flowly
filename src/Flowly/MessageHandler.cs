namespace Flowly;

/// <summary>
///     Base class for message handlers. Inherit from this class and implement the Handle method to create a message
///     handler for a specific message type. Remember to register the handler with the flowly builder.
/// </summary>
/// <typeparam name="TMessage">Type of message handled by this handler</typeparam>
public abstract class MessageHandler<TMessage>
{
    /// <summary>
    ///     Override to configure queue-level options in code. This method is invoked during the discovery of message handlers
    ///     and allows for programmatic configuration of queue options. It can be used in conjunction with or as an alternative
    ///     to attribute-based configuration. The provided HandlerQueueOptions instance can be modified to set various options
    ///     such as queue name, time to live, lock duration, etc. This method is called by the framework during the setup
    ///     phase, and any changes made to the options will be applied when the queue is registered and created. The options
    ///     can also be set using attributes. If you provide both, the values in this Configure method take precedence over
    ///     attribute-based configuration.
    /// </summary>
    /// <param name="options">An instance of HandlerQueueOptions to configure queue options for this message handler.</param>
    public virtual void Configure(HandlerQueueOptions options)
    {
    }

    /// <summary>
    ///     Override to implement the message handling logic. This method is called when a message of type TMessage is received
    ///     on the associated queue. The IMessageContext parameter provides access to the message, as well as metadata and
    ///     services related to the message processing. Implement this method to define how messages of type TMessage should be
    ///     processed by this handler. The framework will take care of invoking this method when messages are received and
    ///     managing the message lifecycle (e.g., completing, abandoning, dead-lettering) based on the outcome of the
    ///     processing logic and any exceptions thrown.
    /// </summary>
    /// <param name="messageContext">Envelope containing the message and related metadata and services.</param>
    /// <returns>A Task representing the asynchronous operation of handling the message.</returns>
    public abstract Task Handle(IMessageContext<TMessage> messageContext);
}