using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessagingAbstractions;

namespace Flowly.Tests.MessageInfrastructure.Events.Telemetry;

public class NullEventHandlerInstrumentationTests
{
    public class IsEnabled
    {
        [Fact]
        public void ReturnsFalse()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            Assert.False(nullEventHandlerInstrumentation.IsEnabled);
        }
    }

    public class StartHandling
    {
        [Fact]
        public void ReturnsNull()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            var activity = nullEventHandlerInstrumentation.StartHandling(
                handlerName: "MyHandler",
                topicOrExchangeName: "orders",
                messagingSystem: "azure-service-bus",
                messageProperties: new MessageProperties(MessageId: "id", CorrelationId: "cid"));

            Assert.Null(activity);
        }
    }

    public class RecordReceived
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            nullEventHandlerInstrumentation.RecordReceived("MyHandler", "orders");
            nullEventHandlerInstrumentation.RecordReceived("MyHandler", "orders", 5);
        }
    }

    public class RecordSucceeded
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            nullEventHandlerInstrumentation.RecordSucceeded("MyHandler", "orders", durationMs: 12.5);
            nullEventHandlerInstrumentation.RecordSucceeded("MyHandler", "orders", durationMs: 12.5, count: 3);
        }
    }

    public class RecordFailed
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            nullEventHandlerInstrumentation.RecordFailed("MyHandler", "orders");
            nullEventHandlerInstrumentation.RecordFailed("MyHandler", "orders", 2);
        }
    }

    public class RecordRetried
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            nullEventHandlerInstrumentation.RecordRetried("MyHandler", "orders");
        }
    }
}
