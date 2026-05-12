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

        private static InMemoryEnvelope BuildEnvelope(string rawBody = "", object? originalMessage = null)
            => new(
                Guid.NewGuid().ToString(),
                rawBody,
                new Dictionary<string, object>(),
                DateTimeOffset.UtcNow,
                OriginalMessage: originalMessage);
    }

    private record TestPayload(string Value);
    private record OtherPayload;
}
