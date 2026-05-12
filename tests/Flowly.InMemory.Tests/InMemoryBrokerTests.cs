namespace Flowly.InMemory.Tests;

public class InMemoryBrokerTests
{
    private static InMemoryBroker CreateBroker() => new(new InMemoryOptions());

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
