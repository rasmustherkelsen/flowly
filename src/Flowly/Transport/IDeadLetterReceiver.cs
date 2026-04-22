namespace Flowly.Transport;

public interface IDeadLetterReceiver : IAsyncDisposable
{
    Task<IReadOnlyCollection<IDeadLetterMessage>> ReceiveMessages(int maxMessages, TimeSpan maxWaitTime, CancellationToken cancellationToken = default);

    Task CompleteMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default);

    Task AbandonMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default);
}