using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace Flowly.AzureServiceBus.Tests;

public class ReceivedMessageNonGenericTests
{
    public class GetBody
    {
        [Fact]
        public void DeserializesBodyIntoTargetType()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(JsonSerializer.Serialize(new OrderPlaced("99"))));
            var receivedMessage = new ReceivedMessage(serviceBusReceivedMessage);

            var body = receivedMessage.GetBody<OrderPlaced>();

            Assert.Equal("99", body.OrderId);
        }

        [Fact]
        public void WhenJsonDeserializesToNull_Throws()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("null"));
            var receivedMessage = new ReceivedMessage(serviceBusReceivedMessage);

            Assert.Throws<InvalidOperationException>(() => receivedMessage.GetBody<OrderPlaced>());
        }
    }

    public class Properties
    {
        [Fact]
        public void PopulatesMessageIdAndCorrelationId()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("{}"),
                messageId: "mid",
                correlationId: "cid");
            var receivedMessage = new ReceivedMessage(serviceBusReceivedMessage);

            Assert.Equal("mid", receivedMessage.Properties.MessageId);
            Assert.Equal("cid", receivedMessage.Properties.CorrelationId);
        }

        [Fact]
        public void PopulatesSessionId()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("{}"),
                sessionId: "session-1");
            var receivedMessage = new ReceivedMessage(serviceBusReceivedMessage);

            Assert.Equal("session-1", receivedMessage.Properties.SessionId);
        }
    }

    private record OrderPlaced(string OrderId);
}
