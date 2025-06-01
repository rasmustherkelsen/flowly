namespace SimpleTransit.MessageInfrastructure.Model;

public interface IBatchMessageContext<T>
{
    IReadOnlyCollection<T> Messages { get; }

    CancellationToken CancellationToken { get; }
}