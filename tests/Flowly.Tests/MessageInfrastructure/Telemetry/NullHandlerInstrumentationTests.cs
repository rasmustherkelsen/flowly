using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.MessageInfrastructure.Telemetry;

public class NullHandlerInstrumentationTests
{
    public class IsEnabled
    {
        [Fact]
        public void ReturnsFalse()
        {
            var nullHandlerInstrumentation = new NullHandlerInstrumentation();

            Assert.False(nullHandlerInstrumentation.IsEnabled);
        }
    }

    public class StartHandling
    {
        [Fact]
        public void ReturnsNull()
        {
            var nullHandlerInstrumentation = new NullHandlerInstrumentation();

            var activity = nullHandlerInstrumentation.StartHandling(
                handlerName: "MyHandler",
                queueName: "my-queue",
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
            var nullHandlerInstrumentation = new NullHandlerInstrumentation();

            nullHandlerInstrumentation.RecordReceived("MyHandler", "my-queue");
            nullHandlerInstrumentation.RecordReceived("MyHandler", "my-queue", 5);
        }
    }

    public class RecordSucceeded
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullHandlerInstrumentation = new NullHandlerInstrumentation();

            nullHandlerInstrumentation.RecordSucceeded("MyHandler", "my-queue", durationMs: 12.5);
            nullHandlerInstrumentation.RecordSucceeded("MyHandler", "my-queue", durationMs: 12.5, count: 3);
        }
    }

    public class RecordFailed
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullHandlerInstrumentation = new NullHandlerInstrumentation();

            nullHandlerInstrumentation.RecordFailed("MyHandler", "my-queue");
            nullHandlerInstrumentation.RecordFailed("MyHandler", "my-queue", 2);
        }
    }

    public class RecordRetried
    {
        [Fact]
        public void DoesNotThrow()
        {
            var nullHandlerInstrumentation = new NullHandlerInstrumentation();

            nullHandlerInstrumentation.RecordRetried("MyHandler", "my-queue");
        }
    }
}
