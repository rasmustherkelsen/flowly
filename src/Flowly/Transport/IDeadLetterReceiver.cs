namespace Flowly.Transport;

/// <summary>
///     Pull-based receiver for draining dead-letter sub-queues. Created by
///     <see cref="IMessageBusClient.CreateDeadLetterReceiver" /> or
///     <see cref="IEventCapableMessageBusClient.CreateEventSubscriptionDeadLetterReceiver" /> and used by the dead-letter
///     tracking background service to move dead-lettered messages into the persistent <c>DeadLetters</c> database table.
/// </summary>
public interface IDeadLetterReceiver : IAsyncDisposable
{
    /// <summary>
    ///     Attempts to receive up to <paramref name="maxMessages" /> dead-lettered messages within
    ///     <paramref name="maxWaitTime" />. Messages remain locked on the broker until
    ///     <see cref="CompleteMessage" /> or <see cref="AbandonMessage" /> is called.
    /// </summary>
    /// <param name="maxMessages">The maximum number of dead-letter messages to receive in one call.</param>
    /// <param name="maxWaitTime">The maximum time to wait before returning a partial or empty batch.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A collection of dead-letter messages, possibly empty if none arrived within the wait time.</returns>
    Task<IReadOnlyCollection<IDeadLetterMessage>> ReceiveMessages(int maxMessages, TimeSpan maxWaitTime, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Acknowledges that <paramref name="message" /> has been successfully persisted, removing it from the
    ///     dead-letter sub-queue. Called by the framework after the message has been written to the database.
    /// </summary>
    /// <param name="message">The dead-letter message to complete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task CompleteMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns <paramref name="message" /> to the dead-letter sub-queue without completing it, typically after a
    ///     transient failure (e.g. database unavailable). The message will be re-delivered on the next polling cycle.
    /// </summary>
    /// <param name="message">The dead-letter message to abandon.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task AbandonMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default);
}