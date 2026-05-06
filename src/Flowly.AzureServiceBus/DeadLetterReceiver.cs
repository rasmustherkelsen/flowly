using Azure.Messaging.ServiceBus;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal class ServiceBusDeadLetterReceiver(ServiceBusReceiver receiver) : IDeadLetterReceiver
{
    public async Task<IReadOnlyCollection<IDeadLetterMessage>> ReceiveMessages(int maxMessages, TimeSpan maxWaitTime, CancellationToken cancellationToken = default)
    {
        var messages = await receiver.ReceiveMessagesAsync(maxMessages, maxWaitTime, cancellationToken);
        return messages.Select(m => (IDeadLetterMessage)new DeadLetterReceivedMessage(m)).ToList();
    }

    public Task CompleteMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        return receiver.CompleteMessageAsync(((DeadLetterReceivedMessage)message).ServiceBusReceivedMessage, cancellationToken);
    }

    public Task AbandonMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        return receiver.AbandonMessageAsync(((DeadLetterReceivedMessage)message).ServiceBusReceivedMessage, cancellationToken: cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return receiver.DisposeAsync();
    }
}