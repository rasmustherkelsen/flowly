namespace Flowly.Transport;

/// <summary>
///     Pull-based receiver for batch message processing. Created by
///     <see cref="IMessageBusClient.CreateReceiver" /> and used by the
///     <see cref="BatchMessageHandler{TMessage}" /> background service to poll for a configurable number of messages,
///     hand them to the handler as a batch, and then acknowledge the whole batch at once via
///     <see cref="CompleteMessages{TMessage}" />.
/// </summary>
public interface IMessageBusReceiver : IAsyncDisposable
{
    /// <summary>
    ///     Attempts to receive up to <paramref name="maxMessagesBeforeProcessing" /> messages within
    ///     <paramref name="maxWaitTime" />. Returns as soon as the maximum is reached or the wait time expires, whichever
    ///     comes first. The returned messages remain locked on the broker until
    ///     <see cref="CompleteMessages{TMessage}" /> is called.
    /// </summary>
    /// <param name="maxMessagesBeforeProcessing">The maximum number of messages to receive in one call.</param>
    /// <param name="maxWaitTime">The maximum time to wait for messages before returning an empty or partial batch.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <typeparam name="TMessage">The expected message body type; used to deserialise the raw payload.</typeparam>
    /// <returns>A collection of received messages, possibly empty if no messages arrived within the wait time.</returns>
    Task<IReadOnlyCollection<IReceivedMessage<TMessage>>> ReceiveMessages<TMessage>(int maxMessagesBeforeProcessing, TimeSpan maxWaitTime, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Acknowledges successful processing of all messages in the batch, causing the broker to remove them from the
    ///     queue. Called by the framework after the <see cref="BatchMessageHandler{TMessage}" /> has processed the batch
    ///     without throwing.
    /// </summary>
    /// <param name="messages">The collection of messages to complete, as returned by <see cref="ReceiveMessages{TMessage}" />.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <typeparam name="TMessage">The message body type.</typeparam>
    Task CompleteMessages<TMessage>(IReadOnlyCollection<IReceivedMessage<TMessage>> messages, CancellationToken cancellationToken = default);
}