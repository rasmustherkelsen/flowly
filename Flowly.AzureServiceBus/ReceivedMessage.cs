using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class ReceivedMessage<TMessage>(ServiceBusReceivedMessage serviceBusReceivedMessage) : IReceivedMessage<TMessage>
{
    private TMessage? _body = default;

    public TMessage Body => _body 
        ??= ServiceBusReceivedMessage.Body.ToObjectFromJson<TMessage>() 
        ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

    public MessageProperties Properties { get; } = new(serviceBusReceivedMessage.MessageId, serviceBusReceivedMessage.CorrelationId);

    public ServiceBusReceivedMessage ServiceBusReceivedMessage { get; } = serviceBusReceivedMessage;
}

internal class ReceivedMessage(ServiceBusReceivedMessage serviceBusReceivedMessage) : IReceivedMessage
{
    public TBody GetBody<TBody>() => serviceBusReceivedMessage.Body.ToObjectFromJson<TBody>()!;

    public MessageProperties Properties { get; } = new(serviceBusReceivedMessage.MessageId, serviceBusReceivedMessage.CorrelationId);
}