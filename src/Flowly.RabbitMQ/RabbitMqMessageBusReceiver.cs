using System.Threading.Channels;
using Flowly.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessageBusReceiver(IChannel channel, string queueName) : IMessageBusReceiver
{
    private readonly Channel<(ulong DeliveryTag, ReadOnlyMemory<byte> Body, IReadOnlyBasicProperties Properties)> _messageBuffer =
        Channel.CreateUnbounded<(ulong, ReadOnlyMemory<byte>, IReadOnlyBasicProperties)>();

    private readonly SemaphoreSlim _startLock = new(1, 1);
    private string? _consumerTag;

    public async Task<IReadOnlyCollection<IReceivedMessage<TMessage>>> ReceiveMessages<TMessage>(
        int maxMessagesBeforeProcessing,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default)
    {
        await EnsureConsuming(maxMessagesBeforeProcessing, cancellationToken);

        var messages = new List<IReceivedMessage<TMessage>>(maxMessagesBeforeProcessing);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(maxWaitTime);

        while (messages.Count < maxMessagesBeforeProcessing)
            try
            {
                var (deliveryTag, body, properties) = await _messageBuffer.Reader.ReadAsync(timeoutCts.Token);
                messages.Add(new RabbitMqBatchReceivedMessage<TMessage>(channel, deliveryTag, body, properties));
            }
            catch (OperationCanceledException)
            {
                while (messages.Count < maxMessagesBeforeProcessing && _messageBuffer.Reader.TryRead(out var buffered)) messages.Add(new RabbitMqBatchReceivedMessage<TMessage>(channel, buffered.DeliveryTag, buffered.Body, buffered.Properties));

                break;
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
        if (_consumerTag != null)
            try
            {
                await channel.BasicCancelAsync(_consumerTag);
            }
            catch
            {
                /* ignore errors during disposal */
            }

        await channel.CloseAsync();
        await channel.DisposeAsync();
    }

    private async Task EnsureConsuming(int maxMessagesBeforeProcessing, CancellationToken cancellationToken)
    {
        if (_consumerTag != null) return;

        await _startLock.WaitAsync(cancellationToken);

        try
        {
            if (_consumerTag != null) return;

            // Prefetch is sized from the first call's batch size — every caller of this receiver
            // (e.g. BatchProcessingBackgroundService) polls repeatedly with the same constant value,
            // so a single long-lived consumer can be reused across calls instead of re-establishing
            // one (BasicConsumeAsync/BasicCancelAsync) on every poll.
            await channel.BasicQosAsync(0, (ushort)maxMessagesBeforeProcessing, false, cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += (_, args) =>
            {
                _messageBuffer.Writer.TryWrite((args.DeliveryTag, args.Body.ToArray(), args.BasicProperties));
                return Task.CompletedTask;
            };

            _consumerTag = await channel.BasicConsumeAsync(queueName, false, consumer, cancellationToken);
        }
        finally
        {
            _startLock.Release();
        }
    }
}
