namespace Flowly.MessageInfrastructure.Model;

internal class BatchMessageContext<T>(IReadOnlyCollection<T> messages, CancellationToken cancellationToken, IReadOnlyList<MessageProperties>? properties = null) : IBatchMessageContext<T>
{
    public IReadOnlyCollection<T> Messages { get; } = messages;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public IReadOnlyList<MessageProperties> Properties { get; } = properties ?? [.. messages.Select(_ => MessageProperties.Empty)];
}