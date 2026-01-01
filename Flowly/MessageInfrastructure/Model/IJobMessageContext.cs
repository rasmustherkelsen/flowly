namespace Flowly.MessageInfrastructure.Model;

public interface IJobMessageContext<T>
{
    Task SaveState<TState>(TState state) where TState : class;
    T Message { get; }
    CancellationToken CancellationToken { get; }
}