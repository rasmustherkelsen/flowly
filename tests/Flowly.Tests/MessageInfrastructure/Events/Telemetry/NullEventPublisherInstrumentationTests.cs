using Flowly.MessageInfrastructure.Events.Telemetry;

namespace Flowly.Tests.MessageInfrastructure.Events.Telemetry;

public class NullEventPublisherInstrumentationTests
{
    public class IsEnabled
    {
        [Fact]
        public void ReturnsFalse()
        {
            var nullEventPublisherInstrumentation = new NullEventPublisherInstrumentation();

            Assert.False(nullEventPublisherInstrumentation.IsEnabled);
        }
    }

    public class StartRaising
    {
        [Fact]
        public void ReturnsNull()
        {
            var nullEventPublisherInstrumentation = new NullEventPublisherInstrumentation();

            var activity = nullEventPublisherInstrumentation.StartRaising("orders", "azure-service-bus", "message-id");

            Assert.Null(activity);
        }
    }

    public class RecordRaised
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullEventPublisherInstrumentation = new NullEventPublisherInstrumentation();

            nullEventPublisherInstrumentation.RecordRaised("orders", durationMs: 15.0);
        }
    }

    public class RecordFailed
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullEventPublisherInstrumentation = new NullEventPublisherInstrumentation();

            nullEventPublisherInstrumentation.RecordFailed("orders");
        }
    }
}
