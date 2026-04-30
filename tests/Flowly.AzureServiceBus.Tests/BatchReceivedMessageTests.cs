using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace Flowly.AzureServiceBus.Tests;

public class BatchReceivedMessageTests
{
    public class Body
    {
        [Fact]
        public void DeserializesJsonBodyIntoTargetType()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(JsonSerializer.Serialize(new OrderPlaced("42"))),
                messageId: "m1",
                correlationId: "c1");
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: null!, message: serviceBusReceivedMessage);

            var body = batchReceivedMessage.Body;

            Assert.Equal("42", body.OrderId);
        }

        [Fact]
        public void CachesDeserializedBodyOnSecondAccess()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(JsonSerializer.Serialize(new OrderPlaced("42"))));
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: null!, message: serviceBusReceivedMessage);

            var first = batchReceivedMessage.Body;
            var second = batchReceivedMessage.Body;

            Assert.Same(first, second);
        }

        [Fact]
        public void WhenJsonDeserializesToNull_Throws()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("null"));
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: null!, message: serviceBusReceivedMessage);

            Assert.Throws<InvalidOperationException>(() => _ = batchReceivedMessage.Body);
        }
    }

    public class Properties
    {
        [Fact]
        public void PopulatesMessageIdAndCorrelationId()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("{}"),
                messageId: "m-abc",
                correlationId: "c-xyz");
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: null!, message: serviceBusReceivedMessage);

            Assert.Equal("m-abc", batchReceivedMessage.Properties.MessageId);
            Assert.Equal("c-xyz", batchReceivedMessage.Properties.CorrelationId);
        }

        [Fact]
        public void WithRetryCountApplicationProperty_PopulatesRetryCount()
        {
            var applicationProperties = new Dictionary<string, object> { ["flowly-retry-count"] = 2 };

            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("{}"),
                properties: applicationProperties);
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: null!, message: serviceBusReceivedMessage);

            Assert.Equal(2, batchReceivedMessage.Properties.RetryCount);
        }

        [Fact]
        public void WithoutRetryCountApplicationProperty_DefaultsRetryCountToZero()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("{}"));
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: null!, message: serviceBusReceivedMessage);

            Assert.Equal(0, batchReceivedMessage.Properties.RetryCount);
        }
    }

    private record OrderPlaced(string OrderId);
}
