using System.Threading.Channels;
using Flowly.Transport;

namespace Flowly.InMemory;

internal class InMemoryMessageBusReceiver(Channel<InMemoryEnvelope> channel, Channel<InMemoryEnvelope> deadLetterChannel) : IMessageBusReceiver
{
    public async Task<IReadOnlyCollection<IReceivedMessage<TMessage>>> ReceiveMessages<TMessage>(
        int maxMessagesBeforeProcessing,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<IReceivedMessage<TMessage>>(maxMessagesBeforeProcessing);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(maxWaitTime);

        try
        {
            while (messages.Count < maxMessagesBeforeProcessing)
            {
                var envelope = await channel.Reader.ReadAsync(timeoutCts.Token);
                messages.Add(new InMemoryReceivedMessage<TMessage>(envelope, deadLetterChannel));
            }
        }
        catch (OperationCanceledException)
        {
            while (messages.Count < maxMessagesBeforeProcessing && channel.Reader.TryRead(out var buffered))
                messages.Add(new InMemoryReceivedMessage<TMessage>(buffered, deadLetterChannel));
        }

        return messages;
    }

    public async Task CompleteMessages<TMessage>(IReadOnlyCollection<IReceivedMessage<TMessage>> messages, CancellationToken cancellationToken = default)
    {
        foreach (var message in messages)
            await message.Complete(cancellationToken);
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}