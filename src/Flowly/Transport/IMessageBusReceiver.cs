namespace Flowly.Transport;

public interface IMessageBusReceiver : IAsyncDisposable
{
    Task<IReadOnlyCollection<IReceivedMessage<TMessage>>> ReceiveMessages<TMessage>(int maxMessagesBeforeProcessing, TimeSpan maxWaitTime, CancellationToken cancellationToken = default);

    Task CompleteMessages<TMessage>(IReadOnlyCollection<IReceivedMessage<TMessage>> messages, CancellationToken cancellationToken = default);
}