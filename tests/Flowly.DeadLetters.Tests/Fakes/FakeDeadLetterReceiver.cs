using Flowly.Transport;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeDeadLetterReceiver : IDeadLetterReceiver
{
    public List<IDeadLetterMessage> CompletedMessages { get; } = [];
    public List<IDeadLetterMessage> AbandonedMessages { get; } = [];
    public bool Disposed { get; private set; }
    public string? MessageIdToThrowOnComplete { get; set; }

    public Task<IReadOnlyCollection<IDeadLetterMessage>> ReceiveMessages(int maxMessages, TimeSpan maxWaitTime, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task CompleteMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        if (message.MessageId == MessageIdToThrowOnComplete)
            throw new InvalidOperationException("Simulated complete failure");

        CompletedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task AbandonMessage(IDeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        AbandonedMessages.Add(message);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
