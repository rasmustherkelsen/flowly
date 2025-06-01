using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using System.Text;
using System.Text.Json;

namespace SimpleTransit.Test.Utils;

internal static class MessageBusHelper
{
    internal static ProcessMessageEventArgs CreateProcessMessageEventArgs<T>(T message)
    {
        var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        return CreateProcessMessageEventArgs(CreateServiceBusReceivedMessage(messageBody));
    }

    public static ServiceBusReceivedMessage CreateServiceBusReceivedMessage<T>(T message)
    {
        var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        return CreateServiceBusReceivedMessage(messageBody);
    }

    private static ServiceBusReceivedMessage CreateServiceBusReceivedMessage(byte[] body)
    {
        var amqpMessageBody = new AmqpMessageBody([new ReadOnlyMemory<byte>(body)]);

        var amqpAnnotatedMessage = new AmqpAnnotatedMessage(amqpMessageBody);

        amqpAnnotatedMessage.Properties.MessageId = new AmqpMessageId(Guid.NewGuid().ToString());

        return ServiceBusReceivedMessage.FromAmqpMessage(amqpAnnotatedMessage, new BinaryData(new ReadOnlyMemory<byte>(Guid.NewGuid().ToByteArray())));
    }

    private static ProcessMessageEventArgs CreateProcessMessageEventArgs(ServiceBusReceivedMessage receivedMessage)
        => new(receivedMessage, null, CancellationToken.None);

}