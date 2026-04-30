namespace Flowly.DeadLetters;

/// <summary>
///     Represents the status of a dead letter message.
/// </summary>
public enum DeadLetterStatus
{
    /// <summary>
    ///     The message is currently in the dead letter queue and has not been requeued.
    /// </summary>
    Pending,

    /// <summary>
    ///     The message has been requeued back to its original queue or topic subscription.
    /// </summary>
    Requeued
}