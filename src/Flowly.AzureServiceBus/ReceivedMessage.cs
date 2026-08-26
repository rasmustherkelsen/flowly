using Azure.Messaging.ServiceBus;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal class ReceivedMessage<TMessage>(ProcessMessageEventArgs args) : IReceivedMessage<TMessage>
{
    public TMessage Body
    {
        get => field ??= args.Message.Body.ToObjectFromJson<TMessage>() ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");
    } = default;

    public MessageProperties Properties { get; } = ServiceBusReceivedMessagePropertiesMapper.Map(args.Message);

    public async Task Complete(CancellationToken cancellationToken = default)
    {
        try
        {
            await args.CompleteMessageAsync(args.Message, cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
        {
            // The handler already ran successfully before the lock expired; the broker will redeliver the message
            // naturally regardless, so treat this as a no-op rather than letting it surface as a handler failure.
        }
    }

    public async Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await args.DeadLetterMessageAsync(args.Message, reason, cancellationToken: cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
        {
            // See Complete() above — the lock is already gone, so there is nothing further Flowly can correctly do.
        }
    }
}

internal class ReceivedMessage(ServiceBusReceivedMessage serviceBusReceivedMessage) : IReceivedMessage
{
    public TBody GetBody<TBody>()
    {
        return serviceBusReceivedMessage.Body.ToObjectFromJson<TBody>() ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TBody).FullName}.");
    }

    public MessageProperties Properties { get; } = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);
}
