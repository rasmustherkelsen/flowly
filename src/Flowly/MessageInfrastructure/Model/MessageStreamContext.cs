namespace Flowly.MessageInfrastructure.Model;

internal class MessageStreamContext<T>(IReadOnlyCollection<T> messages, CancellationToken cancellationToken, int? partition = null) : IMessageStreamContext<T>
{
    public IReadOnlyCollection<T> Messages { get; } = messages;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public int? Partition { get; } = partition;
}
