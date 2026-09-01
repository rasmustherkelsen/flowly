using Flowly.DeadLetters.Telemetry;

namespace Flowly.DeadLetters.Tests.Telemetry;

public class NullDeadLetterCleanupInstrumentationTests
{
    public class RecordPendingPurged
    {
        [Fact]
        public void DoesNotThrow()
        {
            var instrumentation = new NullDeadLetterCleanupInstrumentation();

            instrumentation.RecordPendingPurged(5);
        }
    }

    public class RecordRequeuedPurged
    {
        [Fact]
        public void DoesNotThrow()
        {
            var instrumentation = new NullDeadLetterCleanupInstrumentation();

            instrumentation.RecordRequeuedPurged(5);
        }
    }
}
