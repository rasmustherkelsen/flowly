using System.Threading.Channels;

namespace Flowly.InMemory.Tests;

public class InMemoryDeadLetterReceiverTests
{
    private static (Channel<InMemoryEnvelope> channel, InMemoryDeadLetterReceiver receiver) CreateReceiver()
    {
        var channel = Channel.CreateUnbounded<InMemoryEnvelope>();
        var receiver = new InMemoryDeadLetterReceiver(channel);
        return (channel, receiver);
    }

    private static InMemoryEnvelope CreateEnvelope(string messageId = "msg-1")
        => new(messageId, "{}", new Dictionary<string, object> { [InMemoryConstants.DeadLetterReasonKey] = "handler-error" }, DateTimeOffset.UtcNow);

    public class ReceiveMessages
    {
        [Fact]
        public async Task ReturnsDeadLetterMessages()
        {
            var (channel, receiver) = CreateReceiver();
            await channel.Writer.WriteAsync(CreateEnvelope("msg-1"));
            await channel.Writer.WriteAsync(CreateEnvelope("msg-2"));

            var messages = await receiver.ReceiveMessages(10, TimeSpan.FromSeconds(1));

            Assert.Equal(2, messages.Count);
        }

        [Fact]
        public async Task PopulatesMessageProperties()
        {
            var (channel, receiver) = CreateReceiver();
            await channel.Writer.WriteAsync(CreateEnvelope("my-id"));

            var messages = await receiver.ReceiveMessages(1, TimeSpan.FromSeconds(1));
            var message = messages.Single();

            Assert.Equal("my-id", message.MessageId);
            Assert.Equal("{}", message.RawBody);
            Assert.Equal("handler-error", message.DeadLetterReason);
        }

        [Fact]
        public async Task ReturnsAvailableMessagesOnTimeout()
        {
            var (channel, receiver) = CreateReceiver();
            await channel.Writer.WriteAsync(CreateEnvelope());

            var messages = await receiver.ReceiveMessages(10, TimeSpan.FromMilliseconds(100));

            Assert.Single(messages);
        }
    }

    public class AbandonMessage
    {
        [Fact]
        public async Task ReturnsMessageToChannel()
        {
            var (channel, receiver) = CreateReceiver();
            await channel.Writer.WriteAsync(CreateEnvelope("msg-1"));

            var messages = await receiver.ReceiveMessages(1, TimeSpan.FromSeconds(1));
            await receiver.AbandonMessage(messages.Single());

            Assert.Equal(1, channel.Reader.Count);
        }
    }

    public class CompleteMessage
    {
        [Fact]
        public async Task RemovesMessageFromChannel()
        {
            var (channel, receiver) = CreateReceiver();
            await channel.Writer.WriteAsync(CreateEnvelope());

            var messages = await receiver.ReceiveMessages(1, TimeSpan.FromSeconds(1));
            await receiver.CompleteMessage(messages.Single());

            Assert.Equal(0, channel.Reader.Count);
        }
    }
}
