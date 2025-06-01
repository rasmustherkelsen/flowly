namespace SimpleTransit.MessageInfrastructure.Model;

internal class BatchMessageContext<T>(IReadOnlyCollection<T> messages, CancellationToken cancellationToken) : IBatchMessageContext<T>
{
    public IReadOnlyCollection<T> Messages { get; } = messages;

    public CancellationToken CancellationToken { get; } = cancellationToken;
}