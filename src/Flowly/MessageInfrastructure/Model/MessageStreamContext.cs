namespace Flowly.MessageInfrastructure.Model;

internal class MessageStreamContext<T>(IReadOnlyCollection<T> messages, CancellationToken cancellationToken) : IMessageStreamContext<T>
{
    public IReadOnlyCollection<T> Messages { get; } = messages;

    public CancellationToken CancellationToken { get; } = cancellationToken;
}
