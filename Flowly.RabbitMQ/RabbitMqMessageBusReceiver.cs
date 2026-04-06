using Flowly.MessagingAbstractions;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessageBusReceiver(IChannel channel, string queueName) : IMessageBusReceiver
{
    public async Task<IReadOnlyCollection<IReceivedMessage<TMessage>>> ReceiveMessages<TMessage>(
        int maxMessagesBeforeProcessing,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<IReceivedMessage<TMessage>>(maxMessagesBeforeProcessing);
        var deadline = DateTimeOffset.UtcNow + maxWaitTime;

        while (messages.Count < maxMessagesBeforeProcessing && DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var result = await channel.BasicGetAsync(queueName, autoAck: false, cancellationToken);
            if (result is null)
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero) break;
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                continue;
            }

            messages.Add(new RabbitMqBatchReceivedMessage<TMessage>(
                channel,
                result.DeliveryTag,
                result.Body,
                result.BasicProperties));
        }

        return messages;
    }

    public async Task CompleteMessages<TMessage>(IReadOnlyCollection<IReceivedMessage<TMessage>> messages, CancellationToken cancellationToken = default)
    {
        foreach (var message in messages)
            await message.Complete(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await channel.CloseAsync();
        await channel.DisposeAsync();
    }
}
