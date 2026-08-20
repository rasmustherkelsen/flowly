using Flowly.MessageInfrastructure.Registration;

namespace Flowly.InMemory.Tests;

public class InMemoryBrokerTests
{
    private static InMemoryBroker CreateBroker() => new(new InMemoryOptions());

    private static (InMemoryBroker broker, StreamQueueManifest manifest) CreateBrokerWithStreamManifest(InMemoryOptions? options = null)
    {
        var manifest = new StreamQueueManifest();
        var broker = new InMemoryBroker(options ?? new InMemoryOptions(), manifest);
        return (broker, manifest);
    }

    public class GetQueue
    {
        [Fact]
        public void ReturnsSameChannelForSameQueueName()
        {
            var broker = CreateBroker();

            var first = broker.GetQueue("orders");
            var second = broker.GetQueue("orders");

            Assert.Same(first, second);
        }

        [Fact]
        public void ReturnsDifferentChannelsForDifferentQueueNames()
        {
            var broker = CreateBroker();

            var orders = broker.GetQueue("orders");
            var payments = broker.GetQueue("payments");

            Assert.NotSame(orders, payments);
        }
    }

    public class GetDeadLetterQueue
    {
        [Fact]
        public void ReturnsDifferentChannelFromMainQueue()
        {
            var broker = CreateBroker();

            var main = broker.GetQueue("orders");
            var dlq = broker.GetDeadLetterQueue("orders");

            Assert.NotSame(main, dlq);
        }
    }

    public class GetSessionChannel
    {
        [Fact]
        public void ReturnsSameChannelForSameQueueAndSession()
        {
            var broker = CreateBroker();

            var first = broker.GetSessionChannel("jobs", "lane-1");
            var second = broker.GetSessionChannel("jobs", "lane-1");

            Assert.Same(first, second);
        }

        [Fact]
        public void ReturnsDifferentChannelsForDifferentSessions()
        {
            var broker = CreateBroker();

            var laneOne = broker.GetSessionChannel("jobs", "lane-1");
            var laneTwo = broker.GetSessionChannel("jobs", "lane-2");

            Assert.NotSame(laneOne, laneTwo);
        }
    }

    public class GetTopicSubscriptionChannels
    {
        [Fact]
        public void WithNoSubscriptions_ReturnsEmpty()
        {
            var broker = CreateBroker();

            var channels = broker.GetTopicSubscriptionChannels("events", null);

            Assert.Empty(channels);
        }

        [Fact]
        public void WithRegisteredSubscriptions_ReturnsAllChannels()
        {
            var broker = CreateBroker();
            broker.GetEventSubscriptionChannel("events", "handler-a");
            broker.GetEventSubscriptionChannel("events", "handler-b");

            var channels = broker.GetTopicSubscriptionChannels("events", null);

            Assert.Equal(2, channels.Count);
        }

        [Fact]
        public void WithTargetSubscription_ReturnsOnlyMatchingChannel()
        {
            var broker = CreateBroker();
            var channelA = broker.GetEventSubscriptionChannel("events", "handler-a");
            broker.GetEventSubscriptionChannel("events", "handler-b");

            var channels = broker.GetTopicSubscriptionChannels("events", "handler-a");

            Assert.Single(channels);
            Assert.Same(channelA, channels[0]);
        }

        [Fact]
        public void WithNonExistentTargetSubscription_ReturnsEmpty()
        {
            var broker = CreateBroker();
            broker.GetEventSubscriptionChannel("events", "handler-a");

            var channels = broker.GetTopicSubscriptionChannels("events", "handler-x");

            Assert.Empty(channels);
        }
    }

    public class DeliverReadyScheduledMessages
    {
        [Fact]
        public async Task WithPastDeliveryTime_DeliversMessageToQueue()
        {
            var broker = CreateBroker();
            var envelope = new InMemoryEnvelope("msg-1", "{}", [], DateTimeOffset.UtcNow);

            broker.EnqueueScheduled(envelope, "orders", ScheduledDestinationType.Queue, DateTimeOffset.UtcNow.AddSeconds(-1));

            broker.DeliverReadyScheduledMessages();

            Assert.True(broker.GetQueue("orders").Reader.TryRead(out var delivered));
            Assert.Equal("msg-1", delivered.MessageId);
        }

        [Fact]
        public void WithFutureDeliveryTime_DoesNotDeliverMessage()
        {
            var broker = CreateBroker();
            var envelope = new InMemoryEnvelope("msg-1", "{}", [], DateTimeOffset.UtcNow);

            broker.EnqueueScheduled(envelope, "orders", ScheduledDestinationType.Queue, DateTimeOffset.UtcNow.AddHours(1));

            broker.DeliverReadyScheduledMessages();

            Assert.False(broker.GetQueue("orders").Reader.TryRead(out _));
        }

        [Fact]
        public async Task DeliversToSessionChannel_WhenTypeIsSession()
        {
            var broker = CreateBroker();
            var envelope = new InMemoryEnvelope("msg-1", "{}", [], DateTimeOffset.UtcNow);

            broker.EnqueueScheduled(envelope, "jobs:lane-1", ScheduledDestinationType.Session, DateTimeOffset.UtcNow.AddSeconds(-1));

            broker.DeliverReadyScheduledMessages();

            Assert.True(broker.GetSessionChannel("jobs", "lane-1").Reader.TryRead(out _));
        }

        [Fact]
        public void WithStreamQueueDestination_AppendsToStreamLogInsteadOfChannel()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null);
            var envelope = new InMemoryEnvelope("msg-1", "{}", [], DateTimeOffset.UtcNow);

            broker.EnqueueScheduled(envelope, "orders", ScheduledDestinationType.Queue, DateTimeOffset.UtcNow.AddSeconds(-1));

            broker.DeliverReadyScheduledMessages();

            Assert.Equal(1, broker.GetOrCreateStreamLog("orders").TailOffset);
            Assert.False(broker.GetQueue("orders").Reader.TryRead(out _));
        }
    }

    public class TrimStreamRetention
    {
        [Fact]
        public void EvictsStaleEntriesAcrossAllStreamLogsWithoutRequiringAnAppend()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", maxAgeSeconds: 60, maxLengthBytes: null);
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(new InMemoryEnvelope("stale", "{}", [], DateTimeOffset.UtcNow.AddSeconds(-120)));

            broker.TrimStreamRetention();

            Assert.Equal(1, log.BaseOffset);
        }
    }

    public class IsStreamQueue
    {
        [Fact]
        public void WithNoManifest_ReturnsFalse()
        {
            var broker = CreateBroker();

            Assert.False(broker.IsStreamQueue("orders"));
        }

        [Fact]
        public void WithUnmarkedQueue_ReturnsFalse()
        {
            var (broker, _) = CreateBrokerWithStreamManifest();

            Assert.False(broker.IsStreamQueue("orders"));
        }

        [Fact]
        public void WithMarkedQueue_ReturnsTrue()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null);

            Assert.True(broker.IsStreamQueue("orders"));
        }
    }

    public class GetOrCreateStreamLog
    {
        [Fact]
        public void ReturnsSameLogForSameQueueName()
        {
            var broker = CreateBroker();

            var first = broker.GetOrCreateStreamLog("orders");
            var second = broker.GetOrCreateStreamLog("orders");

            Assert.Same(first, second);
        }

        [Fact]
        public void ReturnsDifferentLogsForDifferentQueueNames()
        {
            var broker = CreateBroker();

            var orders = broker.GetOrCreateStreamLog("orders");
            var payments = broker.GetOrCreateStreamLog("payments");

            Assert.NotSame(orders, payments);
        }
    }

    public class EnqueueOrAppend
    {
        [Fact]
        public async Task WithStreamQueue_AppendsToStreamLogInsteadOfChannel()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null);
            var envelope = new InMemoryEnvelope("1", "{}", [], DateTimeOffset.UtcNow);

            await broker.EnqueueOrAppend("orders", envelope);

            Assert.Equal(1, broker.GetOrCreateStreamLog("orders").TailOffset);
            Assert.False(broker.GetQueue("orders").Reader.TryRead(out _));
        }

        [Fact]
        public async Task WithClassicQueue_WritesToChannel()
        {
            var broker = CreateBroker();
            var envelope = new InMemoryEnvelope("1", "{}", [], DateTimeOffset.UtcNow);

            await broker.EnqueueOrAppend("orders", envelope);

            Assert.True(broker.GetQueue("orders").Reader.TryRead(out var delivered));
            Assert.Equal("1", delivered!.MessageId);
        }

        [Fact]
        public async Task WithPartitionedStream_AppendsToPartitionLogNotTheUnpartitionedLog()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null, partitionCount: 4);
            var envelope = new InMemoryEnvelope("1", "{}", [], DateTimeOffset.UtcNow);

            await broker.EnqueueOrAppend("orders", envelope, partitionKey: "customer-1");

            Assert.Equal(0, broker.GetOrCreateStreamLog("orders").TailOffset);
        }

        [Fact]
        public async Task WithPartitionedStream_SameKeyAlwaysRoutesToSamePartition()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null, partitionCount: 4);

            for (var i = 0; i < 10; i++)
                await broker.EnqueueOrAppend("orders", new InMemoryEnvelope(i.ToString(), "{}", [], DateTimeOffset.UtcNow), partitionKey: "customer-1");

            var nonEmptyPartitions = Enumerable.Range(0, 4)
                .Select(p => broker.GetOrCreatePartitionedStreamLog("orders", p).TailOffset)
                .Count(tailOffset => tailOffset > 0);

            Assert.Equal(1, nonEmptyPartitions);
        }

        [Fact]
        public async Task WithPartitionedStream_NoKeyDistributesRoundRobin()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null, partitionCount: 4);

            for (var i = 0; i < 8; i++)
                await broker.EnqueueOrAppend("orders", new InMemoryEnvelope(i.ToString(), "{}", [], DateTimeOffset.UtcNow));

            for (var partition = 0; partition < 4; partition++)
                Assert.Equal(2, broker.GetOrCreatePartitionedStreamLog("orders", partition).TailOffset);
        }

        [Fact]
        public async Task WithPartitionedStream_DifferentPartitionsAreIndependentLogs()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null, partitionCount: 2);

            await broker.EnqueueOrAppend("orders", new InMemoryEnvelope("1", "{}", [], DateTimeOffset.UtcNow), partitionKey: null);

            Assert.NotSame(
                broker.GetOrCreatePartitionedStreamLog("orders", 0),
                broker.GetOrCreatePartitionedStreamLog("orders", 1));
        }
    }

    public class GetDeadLetterCount
    {
        [Fact]
        public async Task ReturnsZeroWhenNoDlqExists()
        {
            var broker = CreateBroker();

            var count = broker.GetDeadLetterCount("nonexistent");

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task ReturnsCorrectCountAfterWrite()
        {
            var broker = CreateBroker();
            var envelope = new InMemoryEnvelope("id", "{}", [], DateTimeOffset.UtcNow);
            await broker.GetDeadLetterQueue("orders").Writer.WriteAsync(envelope);

            var count = broker.GetDeadLetterCount("orders");

            Assert.Equal(1, count);
        }
    }
}
