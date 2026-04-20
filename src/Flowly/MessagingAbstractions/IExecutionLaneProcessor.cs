namespace Flowly.MessagingAbstractions;

public interface IExecutionLaneProcessor : IAsyncDisposable
{
    event Func<IReceivedMessage,CancellationToken, Task> ProcessMessage;
    
    event Func<ErrorDetails, Task> ProcessError;
    
    Task StartProcessing(CancellationToken cancellationToken = default);
    
    Task StopProcessing(CancellationToken cancellationToken = default);
}