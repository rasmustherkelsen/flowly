using System.Diagnostics;

namespace Flowly.DeadLetters.Telemetry;

internal sealed class NullDeadLetterOperationInstrumentation : IDeadLetterOperationInstrumentation
{
    public Activity? StartRequeue(string queueName, string messageId, ActivityContext originalContext) => null;

    public Activity? StartDiscard(string queueName, string messageId, ActivityContext originalContext) => null;

    public void RecordRequeued(string queueName) { }

    public void RecordDiscarded(string queueName, DeadLetterDiscardReason reason) { }
}
