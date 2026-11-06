namespace Flowly.DeadLetters;

/// <summary>
///     Thrown by <see cref="IDeadLetterService.Requeue(string, string?, CancellationToken)"/> and <see cref="IDeadLetterService.Discard(string, CancellationToken)"/>
///     when the dead letter exists but its <see cref="IDeadLetter.Status"/> does not allow the operation: a dead letter that is not
///     <see cref="DeadLetterStatus.Pending"/> cannot be requeued, and one that was already requeued cannot be discarded. Nothing is published or deleted.
///     It derives from <see cref="InvalidOperationException"/> so callers that already catch that type keep working; other <see cref="InvalidOperationException"/>
///     failures of the service, such as a missing transport provider or a client that cannot address event subscriptions, are not state conflicts.
/// </summary>
public sealed class DeadLetterStatusConflictException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DeadLetterStatusConflictException"/> class.
    /// </summary>
    /// <param name="message">A description of the dead letter, its current status and the operation that was refused.</param>
    public DeadLetterStatusConflictException(string message)
        : base(message)
    {
    }
}
