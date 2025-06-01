namespace SimpleTransit.MessageInfrastructure.Model;

public interface IMessageContext<T>
{
    T Message { get; }
    CancellationToken CancellationToken { get; }
}