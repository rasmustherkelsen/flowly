using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class ServiceBusMessageBusReceiver(ServiceBusReceiver receiver) : IMessageBusReceiver
{
    public ValueTask DisposeAsync() => receiver.DisposeAsync();

    public async Task<IReadOnlyCollection<IReceivedMessage<TMessage>>> ReceiveMessages<TMessage>(int maxMessagesBeforeProcessing, TimeSpan maxWaitTime, CancellationToken cancellationToken = default)
        => (await receiver.ReceiveMessagesAsync(maxMessagesBeforeProcessing, maxWaitTime, cancellationToken))
            .Select(msg => (IReceivedMessage<TMessage>)new BatchReceivedMessage<TMessage>(receiver, msg))
            .ToList();

    public async Task CompleteMessages<TMessage>(IReadOnlyCollection<IReceivedMessage<TMessage>> messages, CancellationToken cancellationToken = default)
    {
        foreach (var receivedMessage in messages)
        {
            await receivedMessage.Complete(cancellationToken);
        }
    }
}
