namespace SimpleTransit.MessageInfrastructure.Model;

internal class MessageContext<T>(T message, CancellationToken cancellationToken) : IMessageContext<T>
{
    public T Message { get; } = message;

    public CancellationToken CancellationToken { get; } = cancellationToken;
}