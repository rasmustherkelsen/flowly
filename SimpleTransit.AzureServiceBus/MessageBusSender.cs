using System.Text.Json;
using Azure.Messaging.ServiceBus;
using SimpleTransit.MessagingAbstractions;

namespace SimpleTransit.AzureServiceBus;

internal class MessageBusSender(ServiceBusSender serviceBusSender) : IMessageBusSender
{
    public async Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        => await CommonSend(new ServiceBusMessage(JsonSerializer.Serialize(message)), messageProperties, cancellationToken);

    public async Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        => await CommonSend(new ServiceBusMessage(), messageProperties, cancellationToken);

    private async Task CommonSend(ServiceBusMessage serviceBusMessage, MessageProperties  messageProperties, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(messageProperties.MessageId))
            serviceBusMessage.MessageId = messageProperties.MessageId;

        if (!string.IsNullOrEmpty(messageProperties.CorrelationId))
            serviceBusMessage.CorrelationId = messageProperties.CorrelationId;
        
        if(!string.IsNullOrEmpty(messageProperties.SessionId))
            serviceBusMessage.SessionId = messageProperties.SessionId;

        await serviceBusSender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }
}