using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Flowly.AzureServiceBus.Tests.Fakes;

namespace Flowly.AzureServiceBus.Tests;

public class ReceivedMessageTests
{
    public class Body
    {
        [Fact]
        public void DeserializesJsonBodyIntoTargetType()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(JsonSerializer.Serialize(new OrderPlaced("42"))));
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, new FakeServiceBusReceiver(), CancellationToken.None));

            var body = receivedMessage.Body;

            Assert.Equal("42", body.OrderId);
        }

        [Fact]
        public void CachesDeserializedBodyOnSecondAccess()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(JsonSerializer.Serialize(new OrderPlaced("42"))));
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, new FakeServiceBusReceiver(), CancellationToken.None));

            var first = receivedMessage.Body;
            var second = receivedMessage.Body;

            Assert.Same(first, second);
        }

        [Fact]
        public void WhenJsonDeserializesToNull_Throws()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("null"));
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, new FakeServiceBusReceiver(), CancellationToken.None));

            Assert.Throws<InvalidOperationException>(() => _ = receivedMessage.Body);
        }
    }

    public class Properties
    {
        [Fact]
        public void PopulatesMessageIdAndCorrelationId()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "m-abc", correlationId: "c-xyz");
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, new FakeServiceBusReceiver(), CancellationToken.None));

            Assert.Equal("m-abc", receivedMessage.Properties.MessageId);
            Assert.Equal("c-xyz", receivedMessage.Properties.CorrelationId);
        }

        [Fact]
        public void PopulatesTraceparentTracestateReplyToSessionIdAndDeliveryCount()
        {
            var applicationProperties = new Dictionary<string, object>
            {
                ["traceparent"] = "00-trace-01",
                ["tracestate"] = "vendor=value"
            };
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                properties: applicationProperties,
                replyTo: "reply-queue",
                sessionId: "session-1",
                deliveryCount: 4);
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, new FakeServiceBusReceiver(), CancellationToken.None));

            Assert.Equal("00-trace-01", receivedMessage.Properties.Traceparent);
            Assert.Equal("vendor=value", receivedMessage.Properties.Tracestate);
            Assert.Equal("reply-queue", receivedMessage.Properties.ReplyTo);
            Assert.Equal("session-1", receivedMessage.Properties.SessionId);
            Assert.Equal(4, receivedMessage.Properties.DeliveryCount);
        }
    }

    public class Complete
    {
        [Fact]
        public async Task DelegatesToReceiverCompleteMessage()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage();
            var fakeServiceBusReceiver = new FakeServiceBusReceiver();
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, fakeServiceBusReceiver, CancellationToken.None));

            await receivedMessage.Complete();

            Assert.Equal(1, fakeServiceBusReceiver.CompleteMessageCallCount);
        }

        [Fact]
        public async Task WhenMessageLockLost_SwallowsExceptionInsteadOfThrowing()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage();
            var fakeServiceBusReceiver = new FakeServiceBusReceiver
            {
                ExceptionToThrowOnComplete = new ServiceBusException("lock lost", ServiceBusFailureReason.MessageLockLost)
            };
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, fakeServiceBusReceiver, CancellationToken.None));

            await receivedMessage.Complete();
        }

        [Fact]
        public async Task WhenServiceBusExceptionForOtherReason_Rethrows()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage();
            var fakeServiceBusReceiver = new FakeServiceBusReceiver
            {
                ExceptionToThrowOnComplete = new ServiceBusException("service busy", ServiceBusFailureReason.ServiceBusy)
            };
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, fakeServiceBusReceiver, CancellationToken.None));

            await Assert.ThrowsAsync<ServiceBusException>(() => receivedMessage.Complete());
        }
    }

    public class DeadLetter
    {
        [Fact]
        public async Task DelegatesToReceiverDeadLetterMessage()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage();
            var fakeServiceBusReceiver = new FakeServiceBusReceiver();
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, fakeServiceBusReceiver, CancellationToken.None));

            await receivedMessage.DeadLetter("bad-message");

            Assert.Equal(1, fakeServiceBusReceiver.DeadLetterMessageCallCount);
        }

        [Fact]
        public async Task WhenMessageLockLost_SwallowsExceptionInsteadOfThrowing()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage();
            var fakeServiceBusReceiver = new FakeServiceBusReceiver
            {
                ExceptionToThrowOnDeadLetter = new ServiceBusException("lock lost", ServiceBusFailureReason.MessageLockLost)
            };
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, fakeServiceBusReceiver, CancellationToken.None));

            await receivedMessage.DeadLetter("bad-message");
        }

        [Fact]
        public async Task WhenServiceBusExceptionForOtherReason_Rethrows()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage();
            var fakeServiceBusReceiver = new FakeServiceBusReceiver
            {
                ExceptionToThrowOnDeadLetter = new ServiceBusException("service busy", ServiceBusFailureReason.ServiceBusy)
            };
            var receivedMessage = new ReceivedMessage<OrderPlaced>(new ProcessMessageEventArgs(serviceBusReceivedMessage, fakeServiceBusReceiver, CancellationToken.None));

            await Assert.ThrowsAsync<ServiceBusException>(() => receivedMessage.DeadLetter("bad-message"));
        }
    }

    private record OrderPlaced(string OrderId);
}
