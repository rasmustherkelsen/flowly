namespace Flowly.MessageInfrastructure.Model;

public interface IMessageContext<T>
{
    T Message { get; }
    
    CancellationToken CancellationToken { get; }
}