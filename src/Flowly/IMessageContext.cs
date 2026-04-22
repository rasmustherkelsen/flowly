namespace Flowly;

public interface IMessageContext<T>
{
    T Message { get; }

    CancellationToken CancellationToken { get; }
}