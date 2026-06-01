using System.Threading.Channels;
using Flowly.Transport;

namespace Flowly.InMemory.Tests;

public class InMemoryReceivedMessageTests
{
    public class Body
    {
        [Fact]
        public void WithOriginalMessage_ReturnsReferenceWithoutDeserialization()
        {
            var original = new TestPayload("hello");
            var envelope = BuildEnvelope(originalMessage: original);
            var deadLetterChannel = Channel.CreateUnbounded<InMemoryEnvelope>();
            var receivedMessage = new InMemoryReceivedMessage<TestPayload>(envelope, deadLetterChannel);

            Assert.Same(original, receivedMessage.Body);
        }

        [Fact]
        public void WithoutOriginalMessage_DeserializesFromRawBody()
        {
            var envelope = BuildEnvelope(rawBody: """{"Value":"world"}""");
            var deadLetterChannel = Channel.CreateUnbounded<InMemoryEnvelope>();
            var receivedMessage = new InMemoryReceivedMessage<TestPayload>(envelope, deadLetterChannel);

            Assert.Equal("world", receivedMessage.Body.Value);
        }

        [Fact]
        public void WithOriginalMessageOfWrongType_FallsBackToDeserialization()
        {
            var envelope = BuildEnvelope(rawBody: """{"Value":"fallback"}""", originalMessage: new OtherPayload());
            var deadLetterChannel = Channel.CreateUnbounded<InMemoryEnvelope>();
            var receivedMessage = new InMemoryReceivedMessage<TestPayload>(envelope, deadLetterChannel);

            Assert.Equal("fallback", receivedMessage.Body.Value);
        }
    }

    public class Properties
    {
        [Fact]
        public void WithReplyTo_ExtractsReplyToFromApplicationProperties()
        {
            var envelope = BuildEnvelope(applicationProperties: new Dictionary<string, object> { ["replyTo"] = "reply-queue" });
            var deadLetterChannel = Channel.CreateUnbounded<InMemoryEnvelope>();
            var receivedMessage = new InMemoryReceivedMessage<TestPayload>(envelope, deadLetterChannel);

            Assert.Equal("reply-queue", receivedMessage.Properties.ReplyTo);
        }

        [Fact]
        public void WithoutReplyTo_ReplyToIsNull()
        {
            var envelope = BuildEnvelope();
            var deadLetterChannel = Channel.CreateUnbounded<InMemoryEnvelope>();
            var receivedMessage = new InMemoryReceivedMessage<TestPayload>(envelope, deadLetterChannel);

            Assert.Null(receivedMessage.Properties.ReplyTo);
        }
    }

    private static InMemoryEnvelope BuildEnvelope(
        string rawBody = "",
        object? originalMessage = null,
        Dictionary<string, object>? applicationProperties = null)
        => new(
            Guid.NewGuid().ToString(),
            rawBody,
            applicationProperties ?? new Dictionary<string, object>(),
            DateTimeOffset.UtcNow,
            OriginalMessage: originalMessage);

    private record TestPayload(string Value);
    private record OtherPayload;
}
