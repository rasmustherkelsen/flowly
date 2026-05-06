namespace Flowly.DeadLetters;

/// <summary>
///     Defines the contract for a service that manages dead letter messages, allowing retrieval, requeuing, and discarding
///     of messages that have failed processing.
/// </summary>
public interface IDeadLetterService
{
    /// <summary>
    ///     Retrieves all persisted dead letter messages.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>A read-only collection of <see cref="IDeadLetter"/> records.</returns>
    Task<IReadOnlyCollection<IDeadLetter>> GetDeadLetters(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Requeues a dead letter message back to its original queue or topic subscription for reprocessing.
    ///     The message must have <see cref="DeadLetterStatus.Pending"/> status to be requeued.
    ///     Throws <see cref="KeyNotFoundException"/> if no message with <paramref name="messageId"/> exists,
    ///     or <see cref="InvalidOperationException"/> if the message is not in a pending state.
    /// </summary>
    /// <param name="messageId">The unique identifier of the dead letter message to requeue.</param>
    /// <param name="requeuedBy">An optional identifier of the person or process requesting the requeue.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    Task Requeue(string messageId, string? requeuedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Discards a dead letter message, permanently deleting it from the tracking store.
    ///     The message must have <see cref="DeadLetterStatus.Pending"/> status to be discarded.
    ///     Throws <see cref="KeyNotFoundException"/> if no message with <paramref name="messageId"/> exists,
    ///     or <see cref="InvalidOperationException"/> if the message has already been requeued.
    /// </summary>
    /// <param name="messageId">The unique identifier of the dead letter message to discard.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    Task Discard(string messageId, CancellationToken cancellationToken = default);
}