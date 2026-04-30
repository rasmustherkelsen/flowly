namespace Flowly.Jobs;

/// <summary>
///     Base class for handling job messages. Implement this class to create a handler for a specific job message type. The
///     handler will be responsible for processing the job message and performing the necessary actions based on the
///     message content.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public abstract class JobHandler<TMessage> where TMessage : IJobMessage
{
    /// <summary>
    ///     Configures the handler with the provided options. This method can be overridden to set up any necessary
    ///     configuration for the handler.
    /// </summary>
    /// <param name="options"></param>
    public virtual void Configure(HandlerQueueOptions options)
    {
    }

    /// <summary>
    ///     Handles the processing of the job message. This method must be implemented by derived classes to define the logic
    ///     for processing the message.
    /// </summary>
    /// <param name="messageContext">The context of the job message being processed.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public abstract Task Handle(IJobMessageContext<TMessage> messageContext);
}