using Flowly.RabbitMQ.Tests.Fakes;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqStreamStartOffsetResolverTests
{
    public class ResolveStartOffset
    {
        [Fact]
        public async Task WithLastAndDeliveredMessages_ReturnsHighestDeliveredOffsetPlusOne()
        {
            var probe = new FakeStreamOffsetProbe { Result = StreamProbeResult.Delivered(38, 41) };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            var startOffset = await resolver.ResolveStartOffset("orders", null, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(42, startOffset);
        }

        [Fact]
        public async Task WithLastAndEmptyStream_ReturnsZero()
        {
            var probe = new FakeStreamOffsetProbe { Result = StreamProbeResult.Empty };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            var startOffset = await resolver.ResolveStartOffset("orders", null, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(0, startOffset);
        }

        [Fact]
        public async Task WithLastAndInconclusiveProbe_ReturnsNull()
        {
            var probe = new FakeStreamOffsetProbe { Result = StreamProbeResult.Inconclusive };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            var startOffset = await resolver.ResolveStartOffset("orders", null, StartPosition.Last(), CancellationToken.None);

            Assert.Null(startOffset);
        }

        [Fact]
        public async Task WithLast_ProbesTheLastChunkOfTheQueue()
        {
            var probe = new FakeStreamOffsetProbe { Result = StreamProbeResult.Empty };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            await resolver.ResolveStartOffset("orders", null, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(["orders"], probe.LastChunkProbes);
            Assert.Empty(probe.TimestampProbes);
        }

        [Fact]
        public async Task WithLastAndPartition_ProbesThePartitionStream()
        {
            var probe = new FakeStreamOffsetProbe { Result = StreamProbeResult.Empty };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            await resolver.ResolveStartOffset("orders", 3, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(["orders-3"], probe.LastChunkProbes);
        }

        [Fact]
        public async Task WithTimestampAndDeliveredMessages_ReturnsFirstDeliveredOffset()
        {
            var probe = new FakeStreamOffsetProbe { Result = StreamProbeResult.Delivered(17, 29) };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            var startOffset = await resolver.ResolveStartOffset("orders", null, StartPosition.Timestamp(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

            Assert.Equal(17, startOffset);
        }

        [Fact]
        public async Task WithTimestampAndNothingDeliveredOnAnEmptyStream_ReturnsZero()
        {
            var probe = new FakeStreamOffsetProbe { TimestampResult = StreamProbeResult.Empty, Result = StreamProbeResult.Empty };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            var startOffset = await resolver.ResolveStartOffset("orders", null, StartPosition.Timestamp(DateTime.UtcNow), CancellationToken.None);

            Assert.Equal(0, startOffset);
            Assert.Equal(["orders"], probe.LastChunkProbes);
        }

        [Fact]
        public async Task WithTimestampAndNothingDeliveredButMessagesInTheStream_ReturnsNull()
        {
            var probe = new FakeStreamOffsetProbe { TimestampResult = StreamProbeResult.Empty, Result = StreamProbeResult.Delivered(0, 9) };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            var startOffset = await resolver.ResolveStartOffset("orders", null, StartPosition.Timestamp(DateTime.UtcNow), CancellationToken.None);

            Assert.Null(startOffset);
        }

        [Fact]
        public async Task WithTimestampAndNothingDeliveredAndAnInconclusiveLastChunkProbe_ReturnsNull()
        {
            var probe = new FakeStreamOffsetProbe { TimestampResult = StreamProbeResult.Empty, Result = StreamProbeResult.Inconclusive };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            var startOffset = await resolver.ResolveStartOffset("orders", 2, StartPosition.Timestamp(DateTime.UtcNow), CancellationToken.None);

            Assert.Null(startOffset);
            Assert.Equal(["orders-2"], probe.LastChunkProbes);
        }

        [Fact]
        public async Task WithTimestampAndDeliveredMessages_DoesNotProbeTheLastChunk()
        {
            var probe = new FakeStreamOffsetProbe { TimestampResult = StreamProbeResult.Delivered(4, 8) };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            await resolver.ResolveStartOffset("orders", null, StartPosition.Timestamp(DateTime.UtcNow), CancellationToken.None);

            Assert.Empty(probe.LastChunkProbes);
        }

        [Fact]
        public async Task WithTimestampAndInconclusiveProbe_ReturnsNull()
        {
            var probe = new FakeStreamOffsetProbe { Result = StreamProbeResult.Inconclusive };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            var startOffset = await resolver.ResolveStartOffset("orders", null, StartPosition.Timestamp(DateTime.UtcNow), CancellationToken.None);

            Assert.Null(startOffset);
        }

        [Fact]
        public async Task WithTimestamp_ProbesFromThatTimestampOnThePartitionStream()
        {
            var probe = new FakeStreamOffsetProbe { TimestampResult = StreamProbeResult.Delivered(3, 5) };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);
            var timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            await resolver.ResolveStartOffset("orders", 1, StartPosition.Timestamp(timestamp), CancellationToken.None);

            Assert.Equal([("orders-1", timestamp)], probe.TimestampProbes);
            Assert.Empty(probe.LastChunkProbes);
        }

        [Fact]
        public async Task WithFirst_ReturnsZeroWithoutProbing()
        {
            var probe = new FakeStreamOffsetProbe();
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            var startOffset = await resolver.ResolveStartOffset("orders", null, StartPosition.First(), CancellationToken.None);

            Assert.Equal(0, startOffset);
            Assert.Empty(probe.LastChunkProbes);
            Assert.Empty(probe.TimestampProbes);
        }

        [Fact]
        public async Task WithOffset_ReturnsTheOffsetWithoutProbing()
        {
            var probe = new FakeStreamOffsetProbe();
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            var startOffset = await resolver.ResolveStartOffset("orders", null, StartPosition.Offset(55), CancellationToken.None);

            Assert.Equal(55, startOffset);
            Assert.Empty(probe.LastChunkProbes);
            Assert.Empty(probe.TimestampProbes);
        }

        [Fact]
        public async Task WithNegativeOffset_ReturnsZero()
        {
            var resolver = new RabbitMqStreamStartOffsetResolver(new FakeStreamOffsetProbe());

            var startOffset = await resolver.ResolveStartOffset("orders", null, StartPosition.Offset(-4), CancellationToken.None);

            Assert.Equal(0, startOffset);
        }

        [Fact]
        public async Task WhenTheProbeThrows_PropagatesTheException()
        {
            var probe = new FakeStreamOffsetProbe { ExceptionToThrow = new InvalidOperationException("channel closed") };
            var resolver = new RabbitMqStreamStartOffsetResolver(probe);

            await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveStartOffset("orders", null, StartPosition.Last(), CancellationToken.None));
        }
    }
}
