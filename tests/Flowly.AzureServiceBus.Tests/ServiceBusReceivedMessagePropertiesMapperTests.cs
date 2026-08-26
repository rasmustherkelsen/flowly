using Azure.Messaging.ServiceBus;

namespace Flowly.AzureServiceBus.Tests;

public class ServiceBusReceivedMessagePropertiesMapperTests
{
    public class Map
    {
        [Fact]
        public void PopulatesMessageIdAndCorrelationId()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "m-abc", correlationId: "c-xyz");

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal("m-abc", messageProperties.MessageId);
            Assert.Equal("c-xyz", messageProperties.CorrelationId);
        }

        [Fact]
        public void PopulatesSessionId()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(sessionId: "session-1");

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal("session-1", messageProperties.SessionId);
        }

        [Fact]
        public void PopulatesDeliveryCount()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(deliveryCount: 4);

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal(4, messageProperties.DeliveryCount);
        }

        [Fact]
        public void PopulatesTraceparentAndTracestateFromApplicationProperties()
        {
            var applicationProperties = new Dictionary<string, object>
            {
                ["traceparent"] = "00-trace-01",
                ["tracestate"] = "vendor=value"
            };
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(properties: applicationProperties);

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal("00-trace-01", messageProperties.Traceparent);
            Assert.Equal("vendor=value", messageProperties.Tracestate);
        }

        [Fact]
        public void WithoutTraceHeaders_LeavesTraceparentAndTracestateNull()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage();

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Null(messageProperties.Traceparent);
            Assert.Null(messageProperties.Tracestate);
        }

        [Fact]
        public void WithReplyTo_PopulatesReplyTo()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(replyTo: "reply-queue");

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal("reply-queue", messageProperties.ReplyTo);
        }

        [Fact]
        public void WithEmptyReplyTo_LeavesReplyToNull()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(replyTo: string.Empty);

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Null(messageProperties.ReplyTo);
        }

        [Fact]
        public void WithoutRetryCountApplicationProperty_DefaultsRetryCountToZero()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage();

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal(0, messageProperties.RetryCount);
        }

        [Fact]
        public void WithIntRetryCountApplicationProperty_PopulatesRetryCount()
        {
            var applicationProperties = new Dictionary<string, object> { ["flowly-retry-count"] = 2 };
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(properties: applicationProperties);

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal(2, messageProperties.RetryCount);
        }

        [Fact]
        public void WithLongRetryCountApplicationProperty_PopulatesRetryCount()
        {
            var applicationProperties = new Dictionary<string, object> { ["flowly-retry-count"] = 3L };
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(properties: applicationProperties);

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal(3, messageProperties.RetryCount);
        }

        [Fact]
        public void WithFourByteLittleEndianRetryCountApplicationProperty_PopulatesRetryCount()
        {
            var applicationProperties = new Dictionary<string, object> { ["flowly-retry-count"] = new byte[] { 5, 0, 0, 0 } };
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(properties: applicationProperties);

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal(5, messageProperties.RetryCount);
        }

        [Fact]
        public void WithMalformedByteArrayRetryCountApplicationProperty_DefaultsRetryCountToZero()
        {
            var applicationProperties = new Dictionary<string, object> { ["flowly-retry-count"] = new byte[] { 1, 2 } };
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(properties: applicationProperties);

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal(0, messageProperties.RetryCount);
        }

        [Fact]
        public void WithUnsupportedRetryCountApplicationPropertyType_DefaultsRetryCountToZeroWithoutThrowing()
        {
            var applicationProperties = new Dictionary<string, object> { ["flowly-retry-count"] = "not-a-number" };
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(properties: applicationProperties);

            var messageProperties = ServiceBusReceivedMessagePropertiesMapper.Map(serviceBusReceivedMessage);

            Assert.Equal(0, messageProperties.RetryCount);
        }
    }
}
