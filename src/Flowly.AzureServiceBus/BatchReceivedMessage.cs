using Azure.Messaging.ServiceBus;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal class BatchReceivedMessage<TMessage>(ServiceBusReceiver receiver, ServiceBusReceivedMessage message) : IReceivedMessage<TMessage>
{
    public TMessage Body => field
        ??= message.Body.ToObjectFromJson<TMessage>()
            ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

    public MessageProperties Properties { get; } = ServiceBusReceivedMessagePropertiesMapper.Map(message);

    public async Task Complete(CancellationToken cancellationToken = default)
    {
        try
        {
            await receiver.CompleteMessageAsync(message, cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
        {
            // The handler already ran successfully before the lock expired; the broker will redeliver the message
            // naturally regardless, so treat this as a no-op rather than letting core mistake it for a batch failure
            // and both republish the batch and re-attempt completion against a lock this process no longer holds.
        }
    }

    public async Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await receiver.DeadLetterMessageAsync(message, reason, cancellationToken: cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
        {
            // See Complete() above — the lock is already gone, so there is nothing further Flowly can correctly do.
        }
    }
}
