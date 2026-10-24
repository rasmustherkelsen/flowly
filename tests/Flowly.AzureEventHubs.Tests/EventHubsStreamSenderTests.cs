using System.Diagnostics;
using System.Text.Json;
using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.Transport;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsStreamSenderTests
{
    private static MessageProperties Properties(string messageId = "message-1", string correlationId = "correlation-1", int retryCount = 0, string? partitionKey = null)
        => new(messageId, correlationId, RetryCount: retryCount, PartitionKey: partitionKey);

    public class SendMessage
    {
        [Fact]
        public async Task SerializesTheMessageAsUtf8JsonBody()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, false);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties());

            Assert.Equal(new Reading("sensor-1", 21), JsonSerializer.Deserialize<Reading>(Assert.Single(producer.Sent).EventData.EventBody));
        }

        [Fact]
        public async Task MapsMessageIdAndCorrelationId()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, false);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties("message-7", "correlation-9"));

            var eventData = Assert.Single(producer.Sent).EventData;
            Assert.Equal("message-7", eventData.MessageId);
            Assert.Equal("correlation-9", eventData.CorrelationId);
        }

        [Fact]
        public async Task WithoutMessageId_GeneratesOne()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, false);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties(messageId: ""));

            Assert.True(Guid.TryParse(Assert.Single(producer.Sent).EventData.MessageId, out _));
        }

        [Fact]
        public async Task WithoutCorrelationId_LeavesItUnset()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, false);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties(correlationId: ""));

            Assert.Null(Assert.Single(producer.Sent).EventData.CorrelationId);
        }

        [Fact]
        public async Task WithRetryCount_SetsRetryCountProperty()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, false);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties(retryCount: 3));

            Assert.Equal(3, Assert.Single(producer.Sent).EventData.Properties[FlowlyMessageProperties.RetryCount]);
        }

        [Fact]
        public async Task WithoutRetryCount_DoesNotSetRetryCountProperty()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, false);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties());

            Assert.DoesNotContain(FlowlyMessageProperties.RetryCount, Assert.Single(producer.Sent).EventData.Properties.Keys);
        }

        [Fact]
        public async Task WithCurrentActivity_PropagatesTraceContext()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, false);
            using var activity = new Activity("send").Start();
            activity.TraceStateString = "vendor=value";

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties());

            var eventData = Assert.Single(producer.Sent).EventData;
            Assert.Equal(activity.Id, eventData.Properties["traceparent"]);
            Assert.Equal("vendor=value", eventData.Properties["tracestate"]);
        }

        [Fact]
        public async Task WithoutCurrentActivity_DoesNotSetTraceContext()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, false);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties());

            Assert.DoesNotContain("traceparent", Assert.Single(producer.Sent).EventData.Properties.Keys);
        }

        [Fact]
        public async Task PartitionedWithKey_SendsWithThePartitionKey()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, true);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties(partitionKey: "sensor-1"));

            Assert.Equal("sensor-1", Assert.Single(producer.Sent).PartitionKey);
        }

        [Fact]
        public async Task PartitionedWithoutKey_SendsWithoutPartitionKeySoTheServiceRoundRobins()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, true);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties());

            Assert.Null(Assert.Single(producer.Sent).PartitionKey);
        }

        [Fact]
        public async Task NotPartitionedWithKey_IgnoresTheKey()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, false);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties(partitionKey: "sensor-1"));

            Assert.Null(Assert.Single(producer.Sent).PartitionKey);
        }

        [Fact]
        public async Task BodyLargerThanMaxMessageSize_ThrowsMessageTooLargeExceptionAndSendsNothing()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, 10, false);

            var exception = await Assert.ThrowsAsync<MessageTooLargeException>(() => eventHubsStreamSender.SendMessage(new Reading("sensor-with-a-long-name", 21), Properties()));

            Assert.Equal("readings", exception.QueueName);
            Assert.Equal(10, exception.MaxSizeBytes);
            Assert.Empty(producer.Sent);
        }

        [Fact]
        public async Task BodyWithinMaxMessageSize_Sends()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, 1024, false);

            await eventHubsStreamSender.SendMessage(new Reading("sensor-1", 21), Properties());

            Assert.Single(producer.Sent);
        }
    }

    public class SendEmptyMessage
    {
        [Fact]
        public async Task ThrowsNotSupportedException()
        {
            var eventHubsStreamSender = new EventHubsStreamSender("readings", new FakeEventHubsProducer(), null, false);

            await Assert.ThrowsAsync<NotSupportedException>(() => eventHubsStreamSender.SendEmptyMessage(Properties()));
        }
    }

    public class SendRawMessage
    {
        [Fact]
        public async Task ThrowsNotSupportedException()
        {
            var eventHubsStreamSender = new EventHubsStreamSender("readings", new FakeEventHubsProducer(), null, false);

            await Assert.ThrowsAsync<NotSupportedException>(() => eventHubsStreamSender.SendRawMessage("{}", new Dictionary<string, object>()));
        }
    }

    private record Reading(string Sensor, int Value);
}
