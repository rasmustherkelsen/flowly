namespace Flowly;

/// <summary>
///     Abstract base class for handling batches of messages of a specific type in the Flowly framework.
///     Override <see cref="Configure" /> to customise queue and batch settings without requiring attributes.
///     Implement <see cref="Handle" /> to define the logic for processing each batch of messages.
/// </summary>
/// <typeparam name="TMessage">The type of message to handle</typeparam>
public abstract class BatchMessageHandler<TMessage>
{
    /// <summary>
    ///     Override to configure queue-level and batch-level options in code. <paramref name="options" /> inherits all
    ///     properties from <see cref="HandlerQueueOptions" /> (queue name, TTL, lock duration, concurrency, etc.) and
    ///     adds batch-specific properties (<see cref="BatchMessageHandlerOptions.MaxMessagesBeforeProcessing" /> and
    ///     <see cref="BatchMessageHandlerOptions.MaxWaitTime" />). Values set here take precedence over any attributes
    ///     applied to the handler class. By default this method does nothing.
    /// </summary>
    /// <param name="options">The options to fill out</param>
    public virtual void Configure(BatchMessageHandlerOptions options)
    {
    }

    /// <summary>
    ///     1. This method is the heart of the BatchMessageHandler. It is responsible for processing a batch of messages of
    ///     type TMessage.
    ///     2. It is called by the Flowly framework when a batch of messages is ready to be processed.
    ///     3. The IBatchMessageContext parameter provides access to the batch of messages and provides methods for
    ///     acknowledging, rejecting, or deferring messages based on the processing outcome.
    /// </summary>
    /// <param name="messageContext">The context for the batch of messages to be processed</param>
    /// <returns>A task representing the asynchronous operation of handling the batch of messages</returns>
    public abstract Task Handle(IBatchMessageContext<TMessage> messageContext);
}