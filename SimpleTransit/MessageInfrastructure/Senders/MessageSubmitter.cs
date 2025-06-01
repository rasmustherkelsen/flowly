using System.Text.Json;
using Azure.Messaging.ServiceBus;
using SimpleTransit.AzureServiceBusWrappers;

namespace SimpleTransit.MessageInfrastructure.Senders;

internal class MessageSubmitter<TMessage>(IServiceBusClient serviceBusClient, MessageSubmitter<TMessage>.QueueSettings queueSettings) : IMessageSubmitter<TMessage>
{
    internal class QueueSettings(string queueName)
    {
        public string QueueName { get; } = queueName;
    }

    public async Task Submit(TMessage message, CancellationToken cancellationToken)
    {
        var sender = serviceBusClient.GetServiceBusSender(queueSettings.QueueName);

        var serviceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(message));

        await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }
}
