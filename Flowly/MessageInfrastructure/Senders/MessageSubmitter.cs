using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Senders;

public class MessageSubmitter<TMessage>(IMessageBusClient messageBusClient, MessageSubmitter<TMessage>.QueueSettings queueSettings) : IMessageSubmitter<TMessage>
{
    public class QueueSettings(string queueName)
    {
        public string QueueName { get; } = queueName;
    }

    public async Task Submit(TMessage message, CancellationToken cancellationToken)
    {
        var sender = messageBusClient.CreateMessageBusSender(queueSettings.QueueName);
        await sender.SendMessage(message, MessageProperties.Empty, cancellationToken);
    }
}
