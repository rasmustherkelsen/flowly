using Flowly.Tool.QueueDiscovery;

namespace Flowly.Tool.Tests.QueueDiscovery;

public class FlowlyQueueDiscoveryResultTests
{
    public class Queues
    {
        [Fact]
        public void WithMultipleQueueDefinitions_ReturnsNames()
        {
            var flowlyQueueDiscoveryResult = new FlowlyQueueDiscoveryResult(
                ConfigurationType: "MyApp.Config",
                QueueDefinitions: new[]
                {
                    new QueueDiscoveryQueue("queue-a", "asb", false, TimeSpan.FromDays(1), true, TimeSpan.FromMinutes(1)),
                    new QueueDiscoveryQueue("queue-b", "asb", true, TimeSpan.FromDays(2), false, TimeSpan.FromMinutes(2))
                },
                EventDefinitions: Array.Empty<QueueDiscoveryEvent>());

            var queues = flowlyQueueDiscoveryResult.Queues;

            Assert.Equal(new[] { "queue-a", "queue-b" }, queues);
        }

        [Fact]
        public void WithNoQueueDefinitions_ReturnsEmpty()
        {
            var flowlyQueueDiscoveryResult = new FlowlyQueueDiscoveryResult(
                ConfigurationType: "MyApp.Config",
                QueueDefinitions: Array.Empty<QueueDiscoveryQueue>(),
                EventDefinitions: Array.Empty<QueueDiscoveryEvent>());

            Assert.Empty(flowlyQueueDiscoveryResult.Queues);
        }
    }

    public class Construction
    {
        [Fact]
        public void PopulatesAllProperties()
        {
            var queue = new QueueDiscoveryQueue("q", "p", false, TimeSpan.FromDays(1), false, TimeSpan.FromMinutes(1));
            var eventDef = new QueueDiscoveryEvent("topic", "sub", "p", TimeSpan.FromDays(1), false);

            var flowlyQueueDiscoveryResult = new FlowlyQueueDiscoveryResult(
                ConfigurationType: "Ns.Config",
                QueueDefinitions: new[] { queue },
                EventDefinitions: new[] { eventDef });

            Assert.Equal("Ns.Config", flowlyQueueDiscoveryResult.ConfigurationType);
            Assert.Single(flowlyQueueDiscoveryResult.QueueDefinitions);
            Assert.Single(flowlyQueueDiscoveryResult.EventDefinitions);
        }
    }

    public class QueueDiscoveryQueueRecord
    {
        [Fact]
        public void PopulatesAllProperties()
        {
            var queueDiscoveryQueue = new QueueDiscoveryQueue(
                Name: "orders",
                ProviderName: "azure-service-bus",
                RequiresSession: true,
                DefaultMessageTimeToLive: TimeSpan.FromDays(3),
                DeadLetterOnMessageExpiration: true,
                LockDuration: TimeSpan.FromMinutes(5));

            Assert.Equal("orders", queueDiscoveryQueue.Name);
            Assert.Equal("azure-service-bus", queueDiscoveryQueue.ProviderName);
            Assert.True(queueDiscoveryQueue.RequiresSession);
            Assert.Equal(TimeSpan.FromDays(3), queueDiscoveryQueue.DefaultMessageTimeToLive);
            Assert.True(queueDiscoveryQueue.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), queueDiscoveryQueue.LockDuration);
        }
    }

    public class QueueDiscoveryEventRecord
    {
        [Fact]
        public void PopulatesAllProperties()
        {
            var queueDiscoveryEvent = new QueueDiscoveryEvent(
                TopicName: "order-events",
                SubscriptionName: "warehouse",
                ProviderName: "azure-service-bus",
                DefaultMessageTimeToLive: TimeSpan.FromDays(1),
                DeadLetterOnMessageExpiration: false);

            Assert.Equal("order-events", queueDiscoveryEvent.TopicName);
            Assert.Equal("warehouse", queueDiscoveryEvent.SubscriptionName);
            Assert.Equal("azure-service-bus", queueDiscoveryEvent.ProviderName);
            Assert.Equal(TimeSpan.FromDays(1), queueDiscoveryEvent.DefaultMessageTimeToLive);
            Assert.False(queueDiscoveryEvent.DeadLetterOnMessageExpiration);
        }
    }
}
