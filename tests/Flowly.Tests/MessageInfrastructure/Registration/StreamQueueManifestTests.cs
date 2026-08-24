using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class StreamQueueManifestTests
{
    public class MarkAsStream
    {
        [Fact]
        public void RegistersQueueAsStream()
        {
            var streamQueueManifest = new StreamQueueManifest();

            streamQueueManifest.MarkAsStream("orders-stream", 3600, 1_000_000);

            Assert.True(streamQueueManifest.IsStreamQueue("orders-stream"));
        }

        [Fact]
        public void IsCaseInsensitive()
        {
            var streamQueueManifest = new StreamQueueManifest();

            streamQueueManifest.MarkAsStream("orders-stream", null, null);

            Assert.True(streamQueueManifest.IsStreamQueue("Orders-Stream"));
        }

        [Fact]
        public void MergesNullWithValue()
        {
            var streamQueueManifest = new StreamQueueManifest();

            streamQueueManifest.MarkAsStream("orders-stream", null, null);
            streamQueueManifest.MarkAsStream("orders-stream", 3600, 1_000_000);

            Assert.True(streamQueueManifest.TryGetRetention("orders-stream", out var retention));
            Assert.Equal(3600, retention.MaxAgeSeconds);
            Assert.Equal(1_000_000, retention.MaxLengthBytes);
        }

        [Fact]
        public void MergesEqualValuesWithoutThrowing()
        {
            var streamQueueManifest = new StreamQueueManifest();

            streamQueueManifest.MarkAsStream("orders-stream", 3600, null);
            streamQueueManifest.MarkAsStream("orders-stream", 3600, null);

            Assert.True(streamQueueManifest.TryGetRetention("orders-stream", out var retention));
            Assert.Equal(3600, retention.MaxAgeSeconds);
        }

        [Fact]
        public void ConflictingValues_Throws()
        {
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("orders-stream", 3600, null);

            var exception = Assert.Throws<InvalidOperationException>(() => streamQueueManifest.MarkAsStream("orders-stream", 7200, null));

            Assert.Contains("orders-stream", exception.Message);
            Assert.Contains(nameof(StreamRetentionSettings.MaxAgeSeconds), exception.Message);
        }

        [Fact]
        public void RecordsPartitionCount()
        {
            var streamQueueManifest = new StreamQueueManifest();

            streamQueueManifest.MarkAsStream("orders-stream", null, null, partitionCount: 4);

            Assert.Equal(4, streamQueueManifest.GetPartitionCount("orders-stream"));
        }

        [Fact]
        public void ConflictingPartitionCount_Throws()
        {
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("orders-stream", null, null, partitionCount: 4);

            var exception = Assert.Throws<InvalidOperationException>(() => streamQueueManifest.MarkAsStream("orders-stream", null, null, partitionCount: 8));

            Assert.Contains("orders-stream", exception.Message);
            Assert.Contains(nameof(StreamRetentionSettings.PartitionCount), exception.Message);
        }
    }

    public class IsStreamQueue
    {
        [Fact]
        public void UnknownQueue_ReturnsFalse()
        {
            var streamQueueManifest = new StreamQueueManifest();

            Assert.False(streamQueueManifest.IsStreamQueue("regular-queue"));
        }
    }

    public class TryGetRetention
    {
        [Fact]
        public void UnknownQueue_ReturnsFalse()
        {
            var streamQueueManifest = new StreamQueueManifest();

            Assert.False(streamQueueManifest.TryGetRetention("regular-queue", out _));
        }
    }

    public class GetPartitionCount
    {
        [Fact]
        public void UnknownQueue_ReturnsNull()
        {
            var streamQueueManifest = new StreamQueueManifest();

            Assert.Null(streamQueueManifest.GetPartitionCount("regular-queue"));
        }

        [Fact]
        public void NonPartitionedStream_ReturnsNull()
        {
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("orders-stream", null, null);

            Assert.Null(streamQueueManifest.GetPartitionCount("orders-stream"));
        }
    }

    public class GetOrCreate
    {
        [Fact]
        public void CreatesAndRegistersSingleton()
        {
            var services = new ServiceCollection();

            var streamQueueManifest = StreamQueueManifest.GetOrCreate(services);

            var registered = services.Single(s => s.ImplementationInstance is StreamQueueManifest).ImplementationInstance;
            Assert.Same(streamQueueManifest, registered);
        }

        [Fact]
        public void ReturnsSameInstanceOnRepeatedCalls()
        {
            var services = new ServiceCollection();

            var first = StreamQueueManifest.GetOrCreate(services);
            var second = StreamQueueManifest.GetOrCreate(services);

            Assert.Same(first, second);
        }
    }
}
