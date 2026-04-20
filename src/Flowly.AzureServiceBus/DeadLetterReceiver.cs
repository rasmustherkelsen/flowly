using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class ServiceBusDeadLetterReceiver(ServiceBusReceiver receiver) : IDeadLetterReceiver
{
    public async Task<IReadOnlyCollection<IDeadLetterMessage>> ReceiveMessages(int maxMessages, TimeSpan maxWaitTime, CancellationToken cancellationToken = default)
    {
        var messages = await receiver.ReceiveMessagesAsync(maxMessages, maxWaitTime, cancellationToken);
        return messages.Select(m => (IDeadLetterMessage)new DeadLetterReceivedMessage(m)).ToList();
    }

    public Task CompleteMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
        => receiver.CompleteMessageAsync(((DeadLetterReceivedMessage)message).ServiceBusReceivedMessage, cancellationToken);

    public Task AbandonMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
        => receiver.AbandonMessageAsync(((DeadLetterReceivedMessage)message).ServiceBusReceivedMessage, cancellationToken: cancellationToken);

    public ValueTask DisposeAsync() => receiver.DisposeAsync();
}
