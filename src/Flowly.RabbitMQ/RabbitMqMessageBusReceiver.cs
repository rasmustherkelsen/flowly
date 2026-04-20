using System.Threading.Channels;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessageBusReceiver(IChannel channel, string queueName) : IMessageBusReceiver
{
    public async Task<IReadOnlyCollection<IReceivedMessage<TMessage>>> ReceiveMessages<TMessage>(
        int maxMessagesBeforeProcessing,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default)
    {
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: (ushort)maxMessagesBeforeProcessing,
            global: false,
            cancellationToken: cancellationToken);

        var messages = new List<IReceivedMessage<TMessage>>(maxMessagesBeforeProcessing);
        var messageBuffer = Channel.CreateBounded<(ulong DeliveryTag, ReadOnlyMemory<byte> Body, IReadOnlyBasicProperties Properties)>(maxMessagesBeforeProcessing);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += (_, args) =>
        {
            messageBuffer.Writer.TryWrite((args.DeliveryTag, args.Body.ToArray(), args.BasicProperties));
            return Task.CompletedTask;
        };

        var consumerTag = await channel.BasicConsumeAsync(queueName, autoAck: false, consumer, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(maxWaitTime);

        try
        {
            while (messages.Count < maxMessagesBeforeProcessing)
            {
                try
                {
                    var (deliveryTag, body, properties) = await messageBuffer.Reader.ReadAsync(timeoutCts.Token);
                    messages.Add(new RabbitMqBatchReceivedMessage<TMessage>(channel, deliveryTag, body, properties));
                }
                catch (OperationCanceledException)
                {
                    while (messages.Count < maxMessagesBeforeProcessing && messageBuffer.Reader.TryRead(out var buffered))
                    {
                        messages.Add(new RabbitMqBatchReceivedMessage<TMessage>(channel, buffered.DeliveryTag, buffered.Body, buffered.Properties));
                    }

                    break;
                }
            }
        }
        finally
        {
            await channel.BasicCancelAsync(consumerTag, cancellationToken: CancellationToken.None);
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
