namespace Flowly;

/// <summary>
///     Options for tracking the lifecycle of dead letter messages, including when to delete requeued and dead-lettered
///     messages.
/// </summary>
public class DeadLetterTrackingOptions
{
    /// <summary>
    ///     The duration after which requeued messages should be automatically deleted from the tracking store. If null,
    ///     requeued messages will not be automatically deleted.
    /// </summary>
    public TimeSpan? DeleteRequeuedMessagesAfter { get; set; }

    /// <summary>
    ///     The duration after which dead-lettered messages should be automatically deleted from the tracking store. If null,
    ///     dead-lettered messages will not be automatically deleted.
    /// </summary>
    public TimeSpan? DeleteDeadLetteredMessagesAfter { get; set; }
}