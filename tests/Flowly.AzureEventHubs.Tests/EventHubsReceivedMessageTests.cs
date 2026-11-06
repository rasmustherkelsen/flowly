using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.Transport;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsReceivedMessageTests
{
    public class Body
    {
        [Fact]
        public void DeserializesTheUtf8JsonBodyTheSenderWrites()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<Reading>(TestEvents.Raw(4, """{"Sensor":"sensor-1","Value":21.5}"""));

            Assert.Equal(new Reading("sensor-1", 21.5), eventHubsReceivedMessage.Body);
        }

        [Fact]
        public void IsNotDeserializedBeforeItIsRead()
        {
            var exception = Record.Exception(() => new EventHubsReceivedMessage<Reading>(TestEvents.Raw(4, "not json")));

            Assert.Null(exception);
        }

        [Fact]
        public void WithMalformedBody_Throws()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<Reading>(TestEvents.Raw(4, "not json"));

            Assert.ThrowsAny<Exception>(() => eventHubsReceivedMessage.Body);
        }

        [Fact]
        public void WithNullBody_ThrowsNamingTheType()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<Reading>(TestEvents.Raw(4, "null"));

            var exception = Assert.Throws<InvalidOperationException>(() => eventHubsReceivedMessage.Body);

            Assert.Contains(nameof(Reading), exception.Message);
        }
    }

    public class Properties
    {
        [Fact]
        public void MapsMessageIdAndCorrelationId()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(TestEvents.Create(1, messageId: "message-1", correlationId: "correlation-1"));

            Assert.Equal("message-1", eventHubsReceivedMessage.Properties.MessageId);
            Assert.Equal("correlation-1", eventHubsReceivedMessage.Properties.CorrelationId);
        }

        [Fact]
        public void WithoutMessageId_GeneratesOne()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(TestEvents.Create(1));

            Assert.True(Guid.TryParse(eventHubsReceivedMessage.Properties.MessageId, out _));
        }

        [Fact]
        public void WithoutCorrelationId_IsEmpty()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(TestEvents.Create(1));

            Assert.Equal(string.Empty, eventHubsReceivedMessage.Properties.CorrelationId);
        }

        [Fact]
        public void StreamOffsetIsTheSequenceNumber()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(TestEvents.Create(41));

            Assert.Equal(41, eventHubsReceivedMessage.Properties.StreamOffset);
        }

        [Fact]
        public void StreamOffsetOfTheFirstEventIsZero()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(TestEvents.Create(0));

            Assert.Equal(0, eventHubsReceivedMessage.Properties.StreamOffset);
        }

        [Fact]
        public void MapsTheTraceContext()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(
                TestEvents.Create(1, properties: new Dictionary<string, object> { ["traceparent"] = "00-trace-span-01", ["tracestate"] = "vendor=value" }));

            Assert.Equal("00-trace-span-01", eventHubsReceivedMessage.Properties.Traceparent);
            Assert.Equal("vendor=value", eventHubsReceivedMessage.Properties.Tracestate);
        }

        [Fact]
        public void MapsStringPropertiesToCustomProperties_AndSkipsFlowlyTraceAndNonTextValues()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(
                TestEvents.Create(
                    1,
                    properties: new Dictionary<string, object>
                    {
                        ["tenant"] = "contoso",
                        [FlowlyMessageProperties.RetryCount] = 2,
                        ["traceparent"] = "00-trace-span-01",
                        ["attempts"] = 4
                    }));

            Assert.Equal(["tenant"], eventHubsReceivedMessage.Properties.CustomProperties.Keys);
            Assert.Equal("contoso", eventHubsReceivedMessage.Properties.CustomProperties["tenant"]);
        }

        [Fact]
        public void WithoutProperties_LeavesCustomPropertiesEmpty()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(TestEvents.Create(1));

            Assert.Empty(eventHubsReceivedMessage.Properties.CustomProperties);
        }

        [Fact]
        public void WithoutTheTraceContext_LeavesItEmpty()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(TestEvents.Create(1));

            Assert.Null(eventHubsReceivedMessage.Properties.Traceparent);
            Assert.Null(eventHubsReceivedMessage.Properties.Tracestate);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(3L)]
        [InlineData("3")]
        public void MapsTheRetryCountFromTheSupportedValueTypes(object retryCount)
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(
                TestEvents.Create(1, properties: new Dictionary<string, object> { [FlowlyMessageProperties.RetryCount] = retryCount }));

            Assert.Equal(3, eventHubsReceivedMessage.Properties.RetryCount);
        }

        [Fact]
        public void WithoutRetryCount_IsZero()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(TestEvents.Create(1));

            Assert.Equal(0, eventHubsReceivedMessage.Properties.RetryCount);
        }

        [Fact]
        public void WithAnUnreadableRetryCount_IsZero()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(
                TestEvents.Create(1, properties: new Dictionary<string, object> { [FlowlyMessageProperties.RetryCount] = "many" }));

            Assert.Equal(0, eventHubsReceivedMessage.Properties.RetryCount);
        }

        [Fact]
        public async Task RoundTripsWhatTheSenderWrites()
        {
            var producer = new FakeEventHubsProducer();
            var eventHubsStreamSender = new EventHubsStreamSender("readings", producer, null, false);

            await eventHubsStreamSender.SendMessage("payload", new MessageProperties("message-1", "correlation-1", RetryCount: 2));
            var sent = Assert.Single(producer.Sent).EventData;
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(sent);

            Assert.Equal("payload", eventHubsReceivedMessage.Body);
            Assert.Equal("message-1", eventHubsReceivedMessage.Properties.MessageId);
            Assert.Equal("correlation-1", eventHubsReceivedMessage.Properties.CorrelationId);
            Assert.Equal(2, eventHubsReceivedMessage.Properties.RetryCount);
        }
    }

    public class Complete
    {
        [Fact]
        public async Task Succeeds()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(TestEvents.Create(1));

            await eventHubsReceivedMessage.Complete();
        }
    }

    public class DeadLetter
    {
        [Fact]
        public async Task IsNotSupported()
        {
            var eventHubsReceivedMessage = new EventHubsReceivedMessage<string>(TestEvents.Create(1));

            await Assert.ThrowsAsync<NotSupportedException>(() => eventHubsReceivedMessage.DeadLetter("reason"));
        }
    }

    private sealed record Reading(string Sensor, double Value);
}
