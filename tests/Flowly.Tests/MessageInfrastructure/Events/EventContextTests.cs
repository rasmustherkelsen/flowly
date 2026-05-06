using Flowly.MessageInfrastructure.Events;

namespace Flowly.Tests.MessageInfrastructure.Events;

public class EventContextTests
{
    public class Constructor
    {
        [Fact]
        public void StoresEventAndAllProperties()
        {
            var enqueuedAt = DateTimeOffset.UtcNow;
            var evt = new OrderPlaced("order-1");

            var eventContext = new EventContext<OrderPlaced>(evt, "msg-1", "corr-1", enqueuedAt);

            Assert.Equal(evt, eventContext.Event);
            Assert.Equal("msg-1", eventContext.MessageId);
            Assert.Equal("corr-1", eventContext.CorrelationId);
            Assert.Equal(enqueuedAt, eventContext.EnqueuedAt);
        }

        [Fact]
        public void WithNullCorrelationId_AllowsNull()
        {
            var eventContext = new EventContext<OrderPlaced>(new OrderPlaced("x"), "msg-1", null, DateTimeOffset.UtcNow);

            Assert.Null(eventContext.CorrelationId);
        }
    }

    private record OrderPlaced(string OrderId);
}
