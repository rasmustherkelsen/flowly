using Flowly.DeadLetters.Telemetry;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeDeadLetterCleanupInstrumentation : IDeadLetterCleanupInstrumentation
{
    public int? PendingPurgedCount { get; private set; }
    public int? RequeuedPurgedCount { get; private set; }

    public void RecordPendingPurged(int count) => PendingPurgedCount = count;

    public void RecordRequeuedPurged(int count) => RequeuedPurgedCount = count;
}
