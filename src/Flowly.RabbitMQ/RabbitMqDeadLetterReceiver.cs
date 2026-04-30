using Flowly.Transport;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqDeadLetterReceiver(IChannel channel, string deadLetterQueueName) : IDeadLetterReceiver
{
    public async Task<IReadOnlyCollection<IDeadLetterMessage>> ReceiveMessages(
        int maxMessages,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<IDeadLetterMessage>(maxMessages);
        var deadline = DateTimeOffset.UtcNow + maxWaitTime;

        while (messages.Count < maxMessages && DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var result = await channel.BasicGetAsync(deadLetterQueueName, false, cancellationToken);
            if (result is null)
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero) break;
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                continue;
            }

            messages.Add(new RabbitMqDeadLetterReceivedMessage(result.DeliveryTag, result.Body, result.BasicProperties));
        }

        return messages;
    }

    public Task CompleteMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        return channel.BasicAckAsync(((RabbitMqDeadLetterReceivedMessage)message).DeliveryTag, false, cancellationToken).AsTask();
    }

    public Task AbandonMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        return channel.BasicNackAsync(((RabbitMqDeadLetterReceivedMessage)message).DeliveryTag, false, true, cancellationToken).AsTask();
    }

    public async ValueTask DisposeAsync()
    {
        await channel.CloseAsync();
        await channel.DisposeAsync();
    }
}