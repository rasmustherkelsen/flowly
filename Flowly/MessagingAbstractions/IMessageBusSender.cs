namespace Flowly.MessagingAbstractions;

public interface IMessageBusSender
{
    Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default);

    Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default);
}