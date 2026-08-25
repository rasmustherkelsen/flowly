using Flowly.RabbitMQ.Tests.Fakes;
using Flowly.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqExecutionLaneProcessorTests
{
    public class StartProcessing
    {
        [Fact]
        public async Task WithReceiveAndDelete_ConsumesWithAutoAck()
        {
            var fakeChannel = new FakeChannel();
            var processor = new RabbitMqExecutionLaneProcessor(fakeChannel, "recurring-jobs", "session-a", new MessageBusProcessorOptions(1, MessageBusReceiveMode.ReceiveAndDelete));

            await processor.StartProcessing();

            Assert.True(fakeChannel.LastAutoAck);
        }

        [Fact]
        public async Task WithReceiveAndDelete_DoesNotAckAfterDispatch()
        {
            var fakeChannel = new FakeChannel();
            var processor = new RabbitMqExecutionLaneProcessor(fakeChannel, "recurring-jobs", "session-a", new MessageBusProcessorOptions(1, MessageBusReceiveMode.ReceiveAndDelete));
            processor.ProcessMessage += (_, _) => Task.CompletedTask;

            await processor.StartProcessing();
            await fakeChannel.DeliverAsync(1);

            Assert.Empty(fakeChannel.AckedDeliveryTags);
        }

        [Fact]
        public async Task WithPeekLock_AcksAfterSuccessfulHandling()
        {
            var fakeChannel = new FakeChannel();
            var processor = new RabbitMqExecutionLaneProcessor(fakeChannel, "recurring-jobs", "session-a", new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock));
            processor.ProcessMessage += (_, _) => Task.CompletedTask;

            await processor.StartProcessing();
            await fakeChannel.DeliverAsync(1);

            Assert.Equal([1UL], fakeChannel.AckedDeliveryTags);
        }

        [Fact]
        public async Task WithPeekLock_WhenHandlerThrows_StillAcksAndRoutesToProcessError()
        {
            var fakeChannel = new FakeChannel();
            var processor = new RabbitMqExecutionLaneProcessor(fakeChannel, "recurring-jobs", "session-a", new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock));
            processor.ProcessMessage += (_, _) => throw new InvalidOperationException("job failed");

            ErrorDetails? capturedError = null;
            processor.ProcessError += error =>
            {
                capturedError = error;
                return Task.CompletedTask;
            };

            await processor.StartProcessing();
            await fakeChannel.DeliverAsync(1);

            Assert.Equal([1UL], fakeChannel.AckedDeliveryTags);
            Assert.NotNull(capturedError);
            Assert.IsType<InvalidOperationException>(capturedError!.Exception);
        }

        [Fact]
        public async Task SetsQosPrefetchToMaxConcurrentCalls()
        {
            var fakeChannel = new FakeChannel();
            var processor = new RabbitMqExecutionLaneProcessor(fakeChannel, "recurring-jobs", "session-a", new MessageBusProcessorOptions(3, MessageBusReceiveMode.PeekLock));

            await processor.StartProcessing();

            Assert.Equal(3, fakeChannel.LastPrefetchCount);
        }
    }

    private class FakeChannel : ChannelStub
    {
        private const string ConsumerTag = "fake-consumer-tag";
        private IAsyncBasicConsumer? _consumer;

        public bool LastAutoAck { get; private set; }
        public ushort LastPrefetchCount { get; private set; }
        public List<ulong> AckedDeliveryTags { get; } = [];

        public async Task DeliverAsync(ulong deliveryTag)
        {
            await _consumer!.HandleBasicDeliverAsync(
                ConsumerTag,
                deliveryTag,
                redelivered: false,
                exchange: "",
                routingKey: "recurring-jobs.lane.session-a",
                properties: new BasicProperties(),
                body: ReadOnlyMemory<byte>.Empty,
                cancellationToken: CancellationToken.None);
        }

        public override Task<QueueDeclareOk> QueueDeclareAsync(
            string queue,
            bool durable,
            bool exclusive,
            bool autoDelete,
            IDictionary<string, object?>? arguments = null,
            bool passive = false,
            bool noWait = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new QueueDeclareOk(queue, 0, 0));

        public override Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default)
        {
            LastPrefetchCount = prefetchCount;
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
            LastAutoAck = autoAck;
            _consumer = consumer;
            return Task.FromResult(ConsumerTag);
        }

        public override ValueTask BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default)
        {
            AckedDeliveryTags.Add(deliveryTag);
            return ValueTask.CompletedTask;
        }

        public override Task BasicCancelAsync(string consumerTag, bool noWait = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
