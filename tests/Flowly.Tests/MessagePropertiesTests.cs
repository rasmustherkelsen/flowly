namespace Flowly.Tests;

public class MessagePropertiesTests
{
    public class Constructor
    {
        [Fact]
        public void WithRequiredArguments_SetsOptionalDefaults()
        {
            var messageProperties = new MessageProperties("msg-1", "corr-1");

            Assert.Equal("msg-1", messageProperties.MessageId);
            Assert.Equal("corr-1", messageProperties.CorrelationId);
            Assert.Null(messageProperties.SessionId);
            Assert.Equal(0, messageProperties.RetryCount);
            Assert.Null(messageProperties.ScheduledEnqueueTime);
            Assert.Null(messageProperties.Traceparent);
            Assert.Null(messageProperties.Tracestate);
            Assert.Null(messageProperties.DeliveryCount);
        }

        [Fact]
        public void WithAllArguments_StoresEachValue()
        {
            var scheduled = DateTimeOffset.UtcNow;
            var messageProperties = new MessageProperties(
                "msg-1",
                "corr-1",
                "sess-1",
                3,
                scheduled,
                "tp",
                "ts",
                DeliveryCount: 7);

            Assert.Equal("sess-1", messageProperties.SessionId);
            Assert.Equal(3, messageProperties.RetryCount);
            Assert.Equal(scheduled, messageProperties.ScheduledEnqueueTime);
            Assert.Equal("tp", messageProperties.Traceparent);
            Assert.Equal("ts", messageProperties.Tracestate);
            Assert.Equal(7, messageProperties.DeliveryCount);
        }
    }

    public class EmptyInstance
    {
        [Fact]
        public void MessageIdIsEmptyString()
        {
            Assert.Equal(string.Empty, MessageProperties.Empty.MessageId);
        }

        [Fact]
        public void CorrelationIdIsEmptyString()
        {
            Assert.Equal(string.Empty, MessageProperties.Empty.CorrelationId);
        }

        [Fact]
        public void SessionIdIsEmptyString()
        {
            Assert.Equal(string.Empty, MessageProperties.Empty.SessionId);
        }

        [Fact]
        public void RetryCountIsZero()
        {
            Assert.Equal(0, MessageProperties.Empty.RetryCount);
        }

        [Fact]
        public void DeliveryCountIsNull()
        {
            Assert.Null(MessageProperties.Empty.DeliveryCount);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var first = new MessageProperties("msg", "corr", "sess", 1);
            var second = new MessageProperties("msg", "corr", "sess", 1);

            Assert.Equal(first, second);
        }
    }
}
