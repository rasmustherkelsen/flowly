using System.Diagnostics;
using Flowly.DeadLetters.Telemetry;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeDeadLetterOperationInstrumentation : IDeadLetterOperationInstrumentation
{
    public List<(string QueueName, string MessageId)> DiscardStarted { get; } = [];
    public List<(string QueueName, DeadLetterDiscardReason Reason)> Discarded { get; } = [];
    public HashSet<string> MessageIdsToThrowFor { get; } = [];

    public Activity? StartRequeue(string queueName, string messageId, ActivityContext originalContext) => null;

    public Activity? StartDiscard(string queueName, string messageId, ActivityContext originalContext)
    {
        if (MessageIdsToThrowFor.Contains(messageId))
            throw new InvalidOperationException($"Simulated telemetry failure for {messageId}");

        DiscardStarted.Add((queueName, messageId));
        return null;
    }

    public void RecordRequeued(string queueName) { }

    public void RecordDiscarded(string queueName, DeadLetterDiscardReason reason) => Discarded.Add((queueName, reason));
}
