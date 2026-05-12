using System.Threading.Channels;
using Flowly.Transport;

namespace Flowly.InMemory;

internal class InMemoryDeadLetterReceiver(Channel<InMemoryEnvelope> channel) : IDeadLetterReceiver
{
    public async Task<IReadOnlyCollection<IDeadLetterMessage>> ReceiveMessages(
        int maxMessages,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<IDeadLetterMessage>(maxMessages);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(maxWaitTime);

        try
        {
            while (messages.Count < maxMessages)
            {
                var envelope = await channel.Reader.ReadAsync(timeoutCts.Token);
                messages.Add(new InMemoryDeadLetterMessage(envelope));
            }
        }
        catch (OperationCanceledException)
        {
            while (messages.Count < maxMessages && channel.Reader.TryRead(out var buffered))
                messages.Add(new InMemoryDeadLetterMessage(buffered));
        }

        return messages;
    }

    public Task CompleteMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task AbandonMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
        => channel.Writer.WriteAsync(((InMemoryDeadLetterMessage)message).Envelope, cancellationToken).AsTask();

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
