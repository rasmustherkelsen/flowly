using Azure.Messaging.ServiceBus;
using SimpleTransit.MessagingAbstractions;

namespace SimpleTransit.AzureServiceBus;

internal class ServiceBusMessageBusReceiver(ServiceBusReceiver receiver) : IMessageBusReceiver
{   
    public ValueTask DisposeAsync() => receiver.DisposeAsync();
    
    public async Task<IReadOnlyCollection<IReceivedMessage<TMessage>>> ReceiveMessages<TMessage>(int maxMessagesBeforeProcessing, TimeSpan maxWaitTime, CancellationToken cancellationToken = default)
        => (await receiver.ReceiveMessagesAsync(maxMessagesBeforeProcessing, maxWaitTime, cancellationToken))
            .Select(msg => new ReceivedMessage<TMessage>(msg))
            .ToList();

    public async Task CompleteMessages<TMessage>(IReadOnlyCollection<IReceivedMessage<TMessage>> messages, CancellationToken cancellationToken = default)
    {
        foreach (var receivedMessage in messages)
        {
            var message = (ReceivedMessage<TMessage>)receivedMessage;
            await receiver.CompleteMessageAsync(message.ServiceBusReceivedMessage, cancellationToken);
        }
    }
}