namespace Flowly.Transport;

public interface IMessageBusProcessor<TMessage> : IAsyncDisposable
{
    event Func<IReceivedMessage<TMessage>, CancellationToken, Task> ProcessMessage;

    event Func<ErrorDetails, Task> ProcessError;

    Task StartProcessingMessages(CancellationToken cancellationToken = default);

    Task StopProcessing(CancellationToken cancellationToken);
}