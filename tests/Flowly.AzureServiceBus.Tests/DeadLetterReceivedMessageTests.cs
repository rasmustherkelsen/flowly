using Azure.Messaging.ServiceBus;

namespace Flowly.AzureServiceBus.Tests;

public class DeadLetterReceivedMessageTests
{
    public class Constructor
    {
        [Fact]
        public void PopulatesAllFieldsFromReceivedMessage()
        {
            var enqueuedTime = DateTimeOffset.Parse("2026-04-19T12:00:00Z");
            var applicationProperties = new Dictionary<string, object>
            {
                ["custom"] = "value",
                ["DeadLetterReason"] = "max-delivery-count-exceeded",
                ["DeadLetterErrorDescription"] = "handler kept throwing"
            };

            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("raw-body-contents"),
                messageId: "msg-123",
                properties: applicationProperties,
                enqueuedTime: enqueuedTime);
            var deadLetterReceivedMessage = new DeadLetterReceivedMessage(serviceBusReceivedMessage);

            Assert.Equal("msg-123", deadLetterReceivedMessage.MessageId);
            Assert.Equal("raw-body-contents", deadLetterReceivedMessage.RawBody);
            Assert.Equal("value", deadLetterReceivedMessage.ApplicationProperties["custom"]);
            Assert.Equal("max-delivery-count-exceeded", deadLetterReceivedMessage.DeadLetterReason);
            Assert.Equal("handler kept throwing", deadLetterReceivedMessage.DeadLetterErrorDescription);
            Assert.Equal(enqueuedTime, deadLetterReceivedMessage.EnqueuedTime);
        }

        [Fact]
        public void WithNoDeadLetterReason_PropertiesAreNull()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("body"),
                messageId: "msg-1");
            var deadLetterReceivedMessage = new DeadLetterReceivedMessage(serviceBusReceivedMessage);

            Assert.Null(deadLetterReceivedMessage.DeadLetterReason);
            Assert.Null(deadLetterReceivedMessage.DeadLetterErrorDescription);
        }
    }
}
