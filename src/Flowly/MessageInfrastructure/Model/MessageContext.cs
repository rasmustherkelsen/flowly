namespace Flowly.MessageInfrastructure.Model;

internal class MessageContext<T>(T message, CancellationToken cancellationToken, MessageProperties? properties = null) : IMessageContext<T>
{
    public T Message { get; } = message;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public MessageProperties Properties { get; } = properties ?? MessageProperties.Empty;
}