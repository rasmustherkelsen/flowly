using Flowly.RabbitMQ.Tests.Fakes;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqStreamOffsetProbeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private static readonly RabbitMqStreamOffsetProbeTimings DeliveringTimings = new(
        TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(30), TimeSpan.FromSeconds(5), 500, 250);

    private static readonly RabbitMqStreamOffsetProbeTimings SilentTimings = new(
        TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(30), TimeSpan.FromSeconds(5), 500, 250);

    private static (FakeRabbitMqConnectionPool Pool, ProbeChannel Channel) Build()
    {
        var channel = new ProbeChannel();
        var pool = new FakeRabbitMqConnectionPool(new FakeConnection(new ChannelStub()), new FakeConnection(channel));

        return (pool, channel);
    }

    public class ProbeLastChunk
    {
        [Fact]
        public async Task AttachesAtTheLastChunkWithoutAutoAckOnTheConsumerConnection()
        {
            var (pool, channel) = Build();
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings);

            var probeTask = probe.ProbeLastChunk("orders", CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await channel.Deliver(1, 4);
            await probeTask.WaitAsync(TestTimeout);

            Assert.Equal("orders", channel.ConsumedQueue);
            Assert.Equal("last", channel.ConsumeArguments?["x-stream-offset"]);
            Assert.False(channel.ConsumedWithAutoAck);
            Assert.Equal(1, pool.ConsumerConnection.CreateChannelCallCount);
            Assert.Equal(0, pool.PublisherConnection.CreateChannelCallCount);
        }

        [Fact]
        public async Task UsesTheConfiguredPrefetch()
        {
            var (pool, channel) = Build();
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings);

            var probeTask = probe.ProbeLastChunk("orders", CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await channel.Deliver(1, 4);
            await probeTask.WaitAsync(TestTimeout);

            Assert.Equal((ushort)500, channel.PrefetchCount);
        }

        [Fact]
        public async Task WithDeliveredMessages_ReturnsFirstAndLastDeliveredOffset()
        {
            var (pool, channel) = Build();
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings);

            var probeTask = probe.ProbeLastChunk("orders", CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await channel.Deliver(1, 38);
            await channel.Deliver(2, 39);
            await channel.Deliver(3, 41);
            var result = await probeTask.WaitAsync(TestTimeout);

            Assert.Equal(StreamProbeResult.Delivered(38, 41), result);
        }

        [Fact]
        public async Task WithDeliveriesOutOfOrder_ReturnsTheMinimumAndMaximumOffset()
        {
            var (pool, channel) = Build();
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings);

            var probeTask = probe.ProbeLastChunk("orders", CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await channel.Deliver(1, 40);
            await channel.Deliver(2, 38);
            await channel.Deliver(3, 39);
            var result = await probeTask.WaitAsync(TestTimeout);

            Assert.Equal(StreamProbeResult.Delivered(38, 40), result);
        }

        [Fact]
        public async Task WithNothingDeliveredAndNoMessagesReported_ReturnsEmpty()
        {
            var (pool, channel) = Build();
            channel.ReportedMessageCount = 0;
            var probe = new RabbitMqStreamOffsetProbe(pool, SilentTimings);

            var result = await probe.ProbeLastChunk("orders", CancellationToken.None);

            Assert.Equal(StreamProbeResult.Empty, result);
            Assert.Equal("orders", channel.MessageCountQueue);
        }

        [Fact]
        public async Task WithNothingDeliveredButMessagesReported_ReturnsInconclusive()
        {
            var (pool, channel) = Build();
            channel.ReportedMessageCount = 5;
            var probe = new RabbitMqStreamOffsetProbe(pool, SilentTimings);

            var result = await probe.ProbeLastChunk("orders", CancellationToken.None);

            Assert.Equal(StreamProbeResult.Inconclusive, result);
        }

        [Fact]
        public async Task WithDeliveriesLackingAnOffsetHeader_TreatsThemAsNothingDelivered()
        {
            var (pool, channel) = Build();
            channel.ReportedMessageCount = 0;
            var probe = new RabbitMqStreamOffsetProbe(pool, SilentTimings);

            var probeTask = probe.ProbeLastChunk("orders", CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await channel.Deliver(1, null);
            var result = await probeTask.WaitAsync(TestTimeout);

            Assert.Equal(StreamProbeResult.Empty, result);
        }

        [Fact]
        public async Task AfterDeliveredMessages_CancelsTheConsumerAndClosesTheChannel()
        {
            var (pool, channel) = Build();
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings);

            var probeTask = probe.ProbeLastChunk("orders", CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await channel.Deliver(1, 4);
            await probeTask.WaitAsync(TestTimeout);

            Assert.Equal([ProbeChannel.ConsumerTag], channel.CancelledConsumerTags);
            Assert.True(channel.WasClosed);
            Assert.True(channel.WasDisposed);
        }

        [Fact]
        public async Task WithNothingDelivered_CancelsTheConsumerAndClosesTheChannel()
        {
            var (pool, channel) = Build();
            var probe = new RabbitMqStreamOffsetProbe(pool, SilentTimings);

            await probe.ProbeLastChunk("orders", CancellationToken.None);

            Assert.Equal([ProbeChannel.ConsumerTag], channel.CancelledConsumerTags);
            Assert.True(channel.WasClosed);
            Assert.True(channel.WasDisposed);
        }

        [Fact]
        public async Task WhenConsumingFails_ThrowsAndStillClosesTheChannel()
        {
            var (pool, channel) = Build();
            channel.ConsumeException = new InvalidOperationException("no such stream");
            var probe = new RabbitMqStreamOffsetProbe(pool, SilentTimings);

            await Assert.ThrowsAsync<InvalidOperationException>(() => probe.ProbeLastChunk("orders", CancellationToken.None));

            Assert.Empty(channel.CancelledConsumerTags);
            Assert.True(channel.WasClosed);
            Assert.True(channel.WasDisposed);
        }

        [Fact]
        public async Task WhenCancellingTheConsumerFails_StillReturnsTheResultAndDisposesTheChannel()
        {
            var (pool, channel) = Build();
            channel.CancelException = new InvalidOperationException("connection lost");
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings);

            var probeTask = probe.ProbeLastChunk("orders", CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await channel.Deliver(1, 6);
            var result = await probeTask.WaitAsync(TestTimeout);

            Assert.Equal(StreamProbeResult.Delivered(6, 6), result);
            Assert.True(channel.WasDisposed);
        }

        [Fact]
        public async Task WhenCancelledWhileWaitingForTheFirstDelivery_ThrowsAndReleasesTheChannel()
        {
            var (pool, channel) = Build();
            using var cancellation = new CancellationTokenSource();
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings);

            var probeTask = probe.ProbeLastChunk("orders", cancellation.Token);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probeTask.WaitAsync(TestTimeout));
            Assert.Equal([ProbeChannel.ConsumerTag], channel.CancelledConsumerTags);
            Assert.True(channel.WasDisposed);
        }

        [Fact]
        public async Task AcknowledgesCumulativelyEveryConfiguredNumberOfDeliveries()
        {
            var (pool, channel) = Build();
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings with { AcknowledgeEvery = 2 });

            var probeTask = probe.ProbeLastChunk("orders", CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            for (ulong deliveryTag = 1; deliveryTag <= 5; deliveryTag++)
            {
                await channel.Deliver(deliveryTag, (long)deliveryTag);
            }

            await probeTask.WaitAsync(TestTimeout);

            Assert.Equal([(2UL, true), (4UL, true)], channel.Acknowledgements);
        }

        [Fact]
        public async Task AfterTheProbeReturned_DoesNotAcknowledgeLateDeliveries()
        {
            var (pool, channel) = Build();
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings with { AcknowledgeEvery = 1 });

            var probeTask = probe.ProbeLastChunk("orders", CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await channel.Deliver(1, 4);
            await probeTask.WaitAsync(TestTimeout);
            var acknowledgementsAtReturn = channel.Acknowledgements.Count;

            await channel.Deliver(2, 5);

            Assert.Equal(acknowledgementsAtReturn, channel.Acknowledgements.Count);
        }
    }

    public class ProbeFromTimestamp
    {
        [Fact]
        public async Task AttachesWithTheAmqpTimestampInUnixSeconds()
        {
            var (pool, channel) = Build();
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings);
            var timestamp = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

            var probeTask = probe.ProbeFromTimestamp("orders-2", timestamp, CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await channel.Deliver(1, 12);
            await probeTask.WaitAsync(TestTimeout);

            var amqpTimestamp = Assert.IsType<AmqpTimestamp>(channel.ConsumeArguments?["x-stream-offset"]);
            Assert.Equal(new DateTimeOffset(timestamp).ToUnixTimeSeconds(), amqpTimestamp.UnixTime);
            Assert.Equal("orders-2", channel.ConsumedQueue);
        }

        [Fact]
        public async Task WithDeliveredMessages_ReturnsFirstAndLastDeliveredOffset()
        {
            var (pool, channel) = Build();
            var probe = new RabbitMqStreamOffsetProbe(pool, DeliveringTimings);

            var probeTask = probe.ProbeFromTimestamp("orders", DateTime.UtcNow, CancellationToken.None);
            await channel.Consuming.Task.WaitAsync(TestTimeout);
            await channel.Deliver(1, 20);
            await channel.Deliver(2, 29);
            var result = await probeTask.WaitAsync(TestTimeout);

            Assert.Equal(StreamProbeResult.Delivered(20, 29), result);
        }

        [Fact]
        public async Task WithNothingDeliveredAndMessagesReported_ReturnsInconclusive()
        {
            var (pool, channel) = Build();
            channel.ReportedMessageCount = 30;
            var probe = new RabbitMqStreamOffsetProbe(pool, SilentTimings);

            var result = await probe.ProbeFromTimestamp("orders", DateTime.UtcNow.AddDays(1), CancellationToken.None);

            Assert.Equal(StreamProbeResult.Inconclusive, result);
        }

        [Fact]
        public async Task WithNothingDeliveredAndNoMessagesReported_ReturnsEmpty()
        {
            var (pool, channel) = Build();
            channel.ReportedMessageCount = 0;
            var probe = new RabbitMqStreamOffsetProbe(pool, SilentTimings);

            var result = await probe.ProbeFromTimestamp("orders", DateTime.UtcNow, CancellationToken.None);

            Assert.Equal(StreamProbeResult.Empty, result);
        }
    }

    private sealed class ProbeChannel : ChannelStub
    {
        public const string ConsumerTag = "probe-consumer-tag";

        private IAsyncBasicConsumer? _consumer;

        public TaskCompletionSource Consuming { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? ConsumedQueue { get; private set; }
        public bool ConsumedWithAutoAck { get; private set; }
        public IDictionary<string, object?>? ConsumeArguments { get; private set; }
        public ushort PrefetchCount { get; private set; }
        public uint ReportedMessageCount { get; set; }
        public string? MessageCountQueue { get; private set; }
        public Exception? ConsumeException { get; set; }
        public Exception? CancelException { get; set; }
        public List<string> CancelledConsumerTags { get; } = [];
        public List<(ulong DeliveryTag, bool Multiple)> Acknowledgements { get; } = [];

        public Task Deliver(ulong deliveryTag, long? offset)
            => _consumer!.HandleBasicDeliverAsync(
                ConsumerTag,
                deliveryTag,
                redelivered: false,
                exchange: string.Empty,
                routingKey: ConsumedQueue!,
                properties: new BasicProperties { Headers = offset is null ? null : new Dictionary<string, object?> { ["x-stream-offset"] = offset.Value } },
                body: ReadOnlyMemory<byte>.Empty,
                cancellationToken: CancellationToken.None);

        public override Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default)
        {
            PrefetchCount = prefetchCount;
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
            if (ConsumeException is not null) return Task.FromException<string>(ConsumeException);

            ConsumedQueue = queue;
            ConsumedWithAutoAck = autoAck;
            ConsumeArguments = arguments;
            _consumer = consumer;
            Consuming.TrySetResult();

            return Task.FromResult(ConsumerTag);
        }

        public override ValueTask BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default)
        {
            Acknowledgements.Add((deliveryTag, multiple));
            return ValueTask.CompletedTask;
        }

        public override Task BasicCancelAsync(string consumerTag, bool noWait = false, CancellationToken cancellationToken = default)
        {
            if (CancelException is not null) return Task.FromException(CancelException);

            CancelledConsumerTags.Add(consumerTag);
            return Task.CompletedTask;
        }

        public override Task<uint> MessageCountAsync(string queue, CancellationToken cancellationToken = default)
        {
            MessageCountQueue = queue;
            return Task.FromResult(ReportedMessageCount);
        }
    }
}
