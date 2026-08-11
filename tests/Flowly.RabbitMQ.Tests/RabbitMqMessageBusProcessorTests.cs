using Flowly.RabbitMQ.Tests.Fakes;
using Flowly.Transport;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqMessageBusProcessorTests
{
    public class StartProcessingMessages
    {
        [Fact]
        public async Task WithoutConsumeArguments_PassesNullArguments()
        {
            var channel = new ConsumeCapturingChannel();
            var processor = new RabbitMqMessageBusProcessor<TestMessage>(channel, "test-queue", new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock));

            await processor.StartProcessingMessages();

            Assert.Equal("test-queue", channel.ReceivedQueue);
            Assert.Null(channel.ReceivedArguments);
        }

        [Fact]
        public async Task WithConsumeArguments_PassesThemThrough()
        {
            var channel = new ConsumeCapturingChannel();
            var consumeArguments = new Dictionary<string, object?> { ["x-stream-offset"] = "first" };
            var processor = new RabbitMqMessageBusProcessor<TestMessage>(channel, "test-queue", new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock), consumeArguments);

            await processor.StartProcessingMessages();

            Assert.Same(consumeArguments, channel.ReceivedArguments);
        }

        [Fact]
        public async Task WithPeekLock_ConsumesWithoutAutoAck()
        {
            var channel = new ConsumeCapturingChannel();
            var processor = new RabbitMqMessageBusProcessor<TestMessage>(channel, "test-queue", new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock));

            await processor.StartProcessingMessages();

            Assert.False(channel.ReceivedAutoAck);
        }

        [Fact]
        public async Task SetsPrefetchFromMaxConcurrentCalls()
        {
            var channel = new ConsumeCapturingChannel();
            var processor = new RabbitMqMessageBusProcessor<TestMessage>(channel, "test-queue", new MessageBusProcessorOptions(5, MessageBusReceiveMode.PeekLock));

            await processor.StartProcessingMessages();

            Assert.Equal((ushort)5, channel.ReceivedPrefetchCount);
        }
    }

    private record TestMessage(string Value);

    private sealed class ConsumeCapturingChannel : ChannelStub
    {
        public string? ReceivedQueue { get; private set; }
        public bool? ReceivedAutoAck { get; private set; }
        public IDictionary<string, object?>? ReceivedArguments { get; private set; }
        public ushort? ReceivedPrefetchCount { get; private set; }

        public override Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default)
        {
            ReceivedPrefetchCount = prefetchCount;
            return Task.CompletedTask;
        }

        public override Task<string> BasicConsumeAsync(
            string queue,
            bool autoAck,
            string consumerTag,
            bool noLocal,
            bool exclusive,
            IDictionary<string, object?>? arguments,
            IAsyncBasicConsumer consumer,
            CancellationToken cancellationToken = default)
        {
            ReceivedQueue = queue;
            ReceivedAutoAck = autoAck;
            ReceivedArguments = arguments;
            return Task.FromResult("consumer-tag");
        }
    }
}
