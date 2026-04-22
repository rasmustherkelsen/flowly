namespace Flowly.Transport;

public interface IMessageBusSender
{
    Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default);

    Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default);

    Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default);
}