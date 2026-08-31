namespace Flowly.DeadLetters.Telemetry;

internal sealed class NullDeadLetterCleanupInstrumentation : IDeadLetterCleanupInstrumentation
{
    public void RecordPendingPurged(int count) { }

    public void RecordRequeuedPurged(int count) { }
}
