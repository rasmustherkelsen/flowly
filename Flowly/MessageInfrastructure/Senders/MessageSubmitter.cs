using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Senders;

internal class MessageSubmitter<TMessage>(IMessageBusClient messageBusClient, MessageSubmitter<TMessage>.QueueSettings queueSettings) : IMessageSubmitter<TMessage>
{
    internal class QueueSettings(string queueName)
    {
        public string QueueName { get; } = queueName;
    }

    public async Task Submit(TMessage message, CancellationToken cancellationToken)
    {
        var sender = messageBusClient.CreateMessageBusSender(queueSettings.QueueName);
        await sender.SendMessage(message, MessageProperties.Empty, cancellationToken);
    }
}
