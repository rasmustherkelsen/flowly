namespace Flowly;

/// <summary>
///     Thrown from a handler, or from a handler step, to reject a message permanently. Flowly does not retry a
///     rejected message regardless of any <see cref="RetryPolicyAttribute" />: the message is dead-lettered
///     immediately with <see cref="Reason" /> as the dead letter reason (for a job handler the job is failed with
///     <see cref="Reason" /> instead). Use it when retrying cannot help, for example when a handler step finds the
///     message invalid or the sender not authorized.
///     <para>
///         Applies to message handlers, event handlers, call handlers and job handlers. It has no effect in batch
///         handlers and message stream handlers, which do not dead-letter individual messages.
///     </para>
///     <para>
///         To mark an exception type that has no dependency on Flowly as non-retryable instead, register it with
///         <see cref="FlowlyOptions.NonRetryable{TException}" />.
///     </para>
/// </summary>
public class MessageRejectedException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MessageRejectedException" /> class.
    /// </summary>
    /// <param name="reason">
    ///     Why the message is rejected. It becomes <see cref="Exception.Message" /> and the dead letter reason.
    /// </param>
    public MessageRejectedException(string reason) : base(reason)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessageRejectedException" /> class with the exception that caused
    ///     the rejection.
    /// </summary>
    /// <param name="reason">
    ///     Why the message is rejected. It becomes <see cref="Exception.Message" /> and the dead letter reason.
    /// </param>
    /// <param name="innerException">The exception that caused the rejection.</param>
    public MessageRejectedException(string reason, Exception innerException) : base(reason, innerException)
    {
    }

    /// <summary>
    ///     Gets the reason the message was rejected. It is the same text as <see cref="Exception.Message" /> and is
    ///     recorded as the dead letter reason.
    /// </summary>
    public string Reason => Message;
}
