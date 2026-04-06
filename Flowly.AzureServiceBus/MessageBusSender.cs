using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class MessageBusSender(ServiceBusSender serviceBusSender) : IMessageBusSender
{
    public async Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        => await CommonSend(new ServiceBusMessage(JsonSerializer.Serialize(message)), messageProperties, cancellationToken);

    public async Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        => await CommonSend(new ServiceBusMessage(), messageProperties, cancellationToken);

    public async Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
    {
        var message = new ServiceBusMessage(BinaryData.FromString(rawBody));

        foreach (var (key, value) in applicationProperties)
        {
            message.ApplicationProperties[key] = value;
        }

        await serviceBusSender.SendMessageAsync(message, cancellationToken);
    }

    private async Task CommonSend(ServiceBusMessage serviceBusMessage, MessageProperties messageProperties, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(messageProperties.MessageId))
            serviceBusMessage.MessageId = messageProperties.MessageId;

        if (!string.IsNullOrEmpty(messageProperties.CorrelationId))
            serviceBusMessage.CorrelationId = messageProperties.CorrelationId;

        if (!string.IsNullOrEmpty(messageProperties.SessionId))
            serviceBusMessage.SessionId = messageProperties.SessionId;

        if (messageProperties.RetryCount > 0)
            serviceBusMessage.ApplicationProperties["flowly-retry-count"] = messageProperties.RetryCount;

        if (messageProperties.ScheduledEnqueueTime.HasValue)
            serviceBusMessage.ScheduledEnqueueTime = messageProperties.ScheduledEnqueueTime.Value;

        await serviceBusSender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }
}