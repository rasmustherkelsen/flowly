using System.Threading.Channels;

namespace Flowly.InMemory.Tests;

public class InMemoryMessageBusReceiverTests
{
    private static (Channel<InMemoryEnvelope> main, Channel<InMemoryEnvelope> dlq, InMemoryMessageBusReceiver receiver) CreateReceiver()
    {
        var main = Channel.CreateUnbounded<InMemoryEnvelope>();
        var dlq = Channel.CreateUnbounded<InMemoryEnvelope>();
        var receiver = new InMemoryMessageBusReceiver(main, dlq);
        return (main, dlq, receiver);
    }

    private static InMemoryEnvelope CreateEnvelope(string value)
        => new(Guid.NewGuid().ToString(), $"{{\"Value\":\"{value}\"}}", [], DateTimeOffset.UtcNow);

    public class ReceiveMessages
    {
        [Fact]
        public async Task ReturnsMessagesUpToMax()
        {
            var (main, _, receiver) = CreateReceiver();
            await main.Writer.WriteAsync(CreateEnvelope("a"));
            await main.Writer.WriteAsync(CreateEnvelope("b"));
            await main.Writer.WriteAsync(CreateEnvelope("c"));

            var messages = await receiver.ReceiveMessages<TestMessage>(2, TimeSpan.FromSeconds(1));

            Assert.Equal(2, messages.Count);
        }

        [Fact]
        public async Task ReturnsAvailableMessagesOnTimeout()
        {
            var (main, _, receiver) = CreateReceiver();
            await main.Writer.WriteAsync(CreateEnvelope("a"));

            var messages = await receiver.ReceiveMessages<TestMessage>(10, TimeSpan.FromMilliseconds(100));

            Assert.Single(messages);
        }

        [Fact]
        public async Task DeserializesMessageBody()
        {
            var (main, _, receiver) = CreateReceiver();
            await main.Writer.WriteAsync(CreateEnvelope("hello"));

            var messages = await receiver.ReceiveMessages<TestMessage>(1, TimeSpan.FromSeconds(1));

            Assert.Equal("hello", messages.Single().Body.Value);
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoMessagesAndTimeout()
        {
            var (_, _, receiver) = CreateReceiver();

            var messages = await receiver.ReceiveMessages<TestMessage>(5, TimeSpan.FromMilliseconds(50));

            Assert.Empty(messages);
        }
    }

    public class CompleteMessages
    {
        [Fact]
        public async Task CompletesWithoutError()
        {
            var (main, _, receiver) = CreateReceiver();
            await main.Writer.WriteAsync(CreateEnvelope("x"));

            var messages = await receiver.ReceiveMessages<TestMessage>(1, TimeSpan.FromSeconds(1));
            await receiver.CompleteMessages(messages);
        }
    }

    private record TestMessage(string Value);
}
