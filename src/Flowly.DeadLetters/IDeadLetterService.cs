using Flowly.DeadLetters.Services;

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
    ///     Retrieves one page of the persisted dead letter messages matching the filters of <paramref name="deadLetterQuery"/>,
    ///     newest first by <see cref="IDeadLetter.DeadLetteredAt"/> with the <see cref="IDeadLetter.MessageId"/> as tie-breaker.
    ///     Prefer this over <see cref="GetDeadLetters(CancellationToken)"/> when only a subset is needed.
    /// </summary>
    /// <remarks>
    ///     The built-in implementation filters, orders and pages in the database, so only the requested page is loaded; a total
    ///     count is only queried when <see cref="DeadLetterQuery.IncludeTotalCount"/> is set. The default implementation of this
    ///     member keeps custom <see cref="IDeadLetterService"/> implementations working: it loads everything through
    ///     <see cref="GetDeadLetters(CancellationToken)"/> and filters, orders and pages in memory with the same result.
    /// </remarks>
    /// <param name="deadLetterQuery">The filters, the page (<see cref="DeadLetterQuery.Skip"/> and <see cref="DeadLetterQuery.Take"/>) and whether the total count is wanted.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>
    ///     A <see cref="DeadLetterPage"/> with the dead letters of the requested page and, when
    ///     <see cref="DeadLetterQuery.IncludeTotalCount"/> is set, the number of dead letters matching the filters.
    /// </returns>
    async Task<DeadLetterPage> QueryDeadLetters(DeadLetterQuery deadLetterQuery, CancellationToken cancellationToken = default)
        => DeadLetterQueryEvaluator.Evaluate(await GetDeadLetters(cancellationToken), deadLetterQuery);

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
    ///     Requeues a dead letter message like <see cref="Requeue(string, string?, CancellationToken)"/>, optionally republishing
    ///     <paramref name="replacementMessageBody"/> instead of the stored body, so a message that failed because of a faulty payload can be
    ///     corrected before it is processed again.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The content type and the application properties of the stored message are republished unchanged, and event subscription
    ///         dead letters are still addressed to the originating subscriber only. The stored <see cref="IDeadLetter.MessageBody"/> is never
    ///         modified: once the republish has succeeded, the replacement body is recorded in <see cref="IDeadLetter.ReplacementMessageBody"/>
    ///         together with the requeue status, so the original and the correction are both kept. When the republish fails, nothing is recorded
    ///         and the dead letter stays <see cref="DeadLetterStatus.Pending"/>.
    ///     </para>
    ///     <para>
    ///         A replacement is rejected with <see cref="InvalidReplacementMessageBodyException"/> when it is empty or whitespace, when it is
    ///         larger than 256 KiB as UTF-8, or when the stored body is valid JSON and the replacement is not. A stored body that is not valid JSON
    ///         only has the empty and size checks applied to its replacement.
    ///     </para>
    ///     <para>
    ///         The default implementation of this member keeps custom <see cref="IDeadLetterService"/> implementations working: without a
    ///         replacement it delegates to <see cref="Requeue(string, string?, CancellationToken)"/>, and with one it throws
    ///         <see cref="NotSupportedException"/> because it cannot honor the replacement. The built-in implementation overrides it.
    ///     </para>
    /// </remarks>
    /// <param name="messageId">The unique identifier of the dead letter message to requeue.</param>
    /// <param name="requeuedBy">An optional identifier of the person or process requesting the requeue.</param>
    /// <param name="replacementMessageBody">
    ///     The body to republish instead of the stored <see cref="IDeadLetter.MessageBody"/>, or <see langword="null"/> to republish the
    ///     stored body as it is. An empty string is a replacement and is rejected, not treated as <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <exception cref="KeyNotFoundException">No message with <paramref name="messageId"/> exists.</exception>
    /// <exception cref="InvalidOperationException">The message is not in a pending state.</exception>
    /// <exception cref="InvalidReplacementMessageBodyException"><paramref name="replacementMessageBody"/> is not an acceptable replacement.</exception>
    /// <exception cref="NotSupportedException">The default implementation was called with a replacement body.</exception>
    Task Requeue(string messageId, string? requeuedBy, string? replacementMessageBody, CancellationToken cancellationToken = default)
        => replacementMessageBody is null
            ? Requeue(messageId, requeuedBy, cancellationToken)
            : throw new NotSupportedException($"{GetType().Name} does not support requeueing a dead letter with a replacement message body.");

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