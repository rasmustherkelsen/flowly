using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Flowly.AzureServiceBus.Tests.Fakes;

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

        [Fact]
        public void PopulatesTraceparentTracestateReplyToSessionIdAndDeliveryCount()
        {
            var applicationProperties = new Dictionary<string, object>
            {
                ["traceparent"] = "00-trace-01",
                ["tracestate"] = "vendor=value"
            };
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("{}"),
                properties: applicationProperties,
                replyTo: "reply-queue",
                sessionId: "session-1",
                deliveryCount: 4);
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: null!, message: serviceBusReceivedMessage);

            Assert.Equal("00-trace-01", batchReceivedMessage.Properties.Traceparent);
            Assert.Equal("vendor=value", batchReceivedMessage.Properties.Tracestate);
            Assert.Equal("reply-queue", batchReceivedMessage.Properties.ReplyTo);
            Assert.Equal("session-1", batchReceivedMessage.Properties.SessionId);
            Assert.Equal(4, batchReceivedMessage.Properties.DeliveryCount);
        }
    }

    public class Complete
    {
        [Fact]
        public async Task DelegatesToReceiverCompleteMessage()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"));
            var fakeServiceBusReceiver = new FakeServiceBusReceiver();
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: fakeServiceBusReceiver, message: serviceBusReceivedMessage);

            await batchReceivedMessage.Complete();

            Assert.Equal(1, fakeServiceBusReceiver.CompleteMessageCallCount);
        }

        [Fact]
        public async Task WhenMessageLockLost_SwallowsExceptionInsteadOfThrowing()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"));
            var fakeServiceBusReceiver = new FakeServiceBusReceiver
            {
                ExceptionToThrowOnComplete = new ServiceBusException("lock lost", ServiceBusFailureReason.MessageLockLost)
            };
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: fakeServiceBusReceiver, message: serviceBusReceivedMessage);

            await batchReceivedMessage.Complete();
        }

        [Fact]
        public async Task WhenServiceBusExceptionForOtherReason_Rethrows()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"));
            var fakeServiceBusReceiver = new FakeServiceBusReceiver
            {
                ExceptionToThrowOnComplete = new ServiceBusException("service busy", ServiceBusFailureReason.ServiceBusy)
            };
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: fakeServiceBusReceiver, message: serviceBusReceivedMessage);

            await Assert.ThrowsAsync<ServiceBusException>(() => batchReceivedMessage.Complete());
        }
    }

    public class DeadLetter
    {
        [Fact]
        public async Task DelegatesToReceiverDeadLetterMessage()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"));
            var fakeServiceBusReceiver = new FakeServiceBusReceiver();
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: fakeServiceBusReceiver, message: serviceBusReceivedMessage);

            await batchReceivedMessage.DeadLetter("bad-message");

            Assert.Equal(1, fakeServiceBusReceiver.DeadLetterMessageCallCount);
        }

        [Fact]
        public async Task WhenMessageLockLost_SwallowsExceptionInsteadOfThrowing()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"));
            var fakeServiceBusReceiver = new FakeServiceBusReceiver
            {
                ExceptionToThrowOnDeadLetter = new ServiceBusException("lock lost", ServiceBusFailureReason.MessageLockLost)
            };
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: fakeServiceBusReceiver, message: serviceBusReceivedMessage);

            await batchReceivedMessage.DeadLetter("bad-message");
        }

        [Fact]
        public async Task WhenServiceBusExceptionForOtherReason_Rethrows()
        {
            var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"));
            var fakeServiceBusReceiver = new FakeServiceBusReceiver
            {
                ExceptionToThrowOnDeadLetter = new ServiceBusException("service busy", ServiceBusFailureReason.ServiceBusy)
            };
            var batchReceivedMessage = new BatchReceivedMessage<OrderPlaced>(receiver: fakeServiceBusReceiver, message: serviceBusReceivedMessage);

            await Assert.ThrowsAsync<ServiceBusException>(() => batchReceivedMessage.DeadLetter("bad-message"));
        }
    }

    private record OrderPlaced(string OrderId);
}
