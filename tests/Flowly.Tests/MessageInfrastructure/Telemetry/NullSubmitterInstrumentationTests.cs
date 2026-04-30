using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.MessageInfrastructure.Telemetry;

public class NullSubmitterInstrumentationTests
{
    public class IsEnabled
    {
        [Fact]
        public void ReturnsFalse()
        {
            var nullSubmitterInstrumentation = new NullSubmitterInstrumentation();

            Assert.False(nullSubmitterInstrumentation.IsEnabled);
        }
    }

    public class StartSending
    {
        [Fact]
        public void ReturnsNull()
        {
            var nullSubmitterInstrumentation = new NullSubmitterInstrumentation();

            var activity = nullSubmitterInstrumentation.StartSending("my-queue", "azure-service-bus", "message-id");

            Assert.Null(activity);
        }
    }

    public class RecordSent
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullSubmitterInstrumentation = new NullSubmitterInstrumentation();

            nullSubmitterInstrumentation.RecordSent("my-queue", durationMs: 15.0);
        }
    }

    public class RecordFailed
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullSubmitterInstrumentation = new NullSubmitterInstrumentation();

            nullSubmitterInstrumentation.RecordFailed("my-queue");
        }
    }
}
