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
        return receiver.CompleteMessageAsync(AsServiceBusMessage(message).ServiceBusReceivedMessage, cancellationToken);
    }

    public Task AbandonMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        return receiver.AbandonMessageAsync(AsServiceBusMessage(message).ServiceBusReceivedMessage, cancellationToken: cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return receiver.DisposeAsync();
    }

    private static DeadLetterReceivedMessage AsServiceBusMessage(IDeadLetterMessage message)
    {
        if (message is DeadLetterReceivedMessage deadLetterReceivedMessage) return deadLetterReceivedMessage;

        throw new ArgumentException(
            $"Expected an {nameof(IDeadLetterMessage)} of type {nameof(DeadLetterReceivedMessage)} but received {message.GetType().Name}.",
            nameof(message));
    }
}