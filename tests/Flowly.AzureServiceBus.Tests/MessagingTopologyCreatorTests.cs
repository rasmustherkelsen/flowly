using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Flowly.Transport;

namespace Flowly.AzureServiceBus.Tests;

public class MessagingTopologyCreatorTests
{
    public class IsEmulatorHost
    {
        [Theory]
        [InlineData("localhost")]
        [InlineData("LOCALHOST")]
        [InlineData("localhost:5671")]
        [InlineData("127.0.0.1")]
        [InlineData("127.0.0.1:5671")]
        [InlineData("::1")]
        [InlineData("[::1]:5671")]
        [InlineData("host.docker.internal")]
        [InlineData("HOST.DOCKER.INTERNAL:5671")]
        public void WithLocalOrDevContainerHost_ReturnsTrue(string fullyQualifiedNamespace)
        {
            Assert.True(MessagingTopologyCreator.IsEmulatorHost(fullyQualifiedNamespace));
        }

        [Theory]
        [InlineData("mybus.servicebus.windows.net")]
        [InlineData("sb-flowly.servicebus.windows.net")]
        public void WithRealNamespace_ReturnsFalse(string fullyQualifiedNamespace)
        {
            Assert.False(MessagingTopologyCreator.IsEmulatorHost(fullyQualifiedNamespace));
        }
    }

    public class CreateTopology
    {
        [Fact]
        public async Task WhenUsingEmulator_ThrowsInvalidOperationException()
        {
            var adminClient = new FakeServiceBusAdministrationClient();
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("localhost"), adminClient);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                messagingTopologyCreator.CreateTopology([new FakeQueueDescription("queue")], CancellationToken.None));
        }

        [Fact]
        public async Task WhenQueueDoesNotExist_CreatesQueue()
        {
            var adminClient = new FakeServiceBusAdministrationClient { QueueExists = false };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            await messagingTopologyCreator.CreateTopology([new FakeQueueDescription("queue")], CancellationToken.None);

            Assert.Equal(1, adminClient.CreateQueueCallCount);
        }

        [Fact]
        public async Task WhenQueueAlreadyExists_DoesNotCreateQueue()
        {
            var adminClient = new FakeServiceBusAdministrationClient { QueueExists = true };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            await messagingTopologyCreator.CreateTopology([new FakeQueueDescription("queue")], CancellationToken.None);

            Assert.Equal(0, adminClient.CreateQueueCallCount);
        }

        [Fact]
        public async Task WhenMultipleQueuesDoNotExist_CreatesEachQueue()
        {
            var adminClient = new FakeServiceBusAdministrationClient { QueueExists = false };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            await messagingTopologyCreator.CreateTopology(
                [new FakeQueueDescription("queue-one"), new FakeQueueDescription("queue-two"), new FakeQueueDescription("queue-three")],
                CancellationToken.None);

            Assert.Equal(3, adminClient.CreateQueueCallCount);
        }

        [Fact]
        public async Task WhenConcurrentCreatorWinsTheRace_SwallowsMessagingEntityAlreadyExists()
        {
            var adminClient = new FakeServiceBusAdministrationClient
            {
                QueueExists = false,
                ThrowOnCreateQueue = new ServiceBusException("already exists", ServiceBusFailureReason.MessagingEntityAlreadyExists)
            };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            await messagingTopologyCreator.CreateTopology([new FakeQueueDescription("queue")], CancellationToken.None);

            Assert.Equal(1, adminClient.CreateQueueCallCount);
        }

        [Fact]
        public async Task WhenCreateFailsForAnUnrelatedReason_Propagates()
        {
            var adminClient = new FakeServiceBusAdministrationClient
            {
                QueueExists = false,
                ThrowOnCreateQueue = new ServiceBusException("quota exceeded", ServiceBusFailureReason.QuotaExceeded)
            };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            var exception = await Assert.ThrowsAsync<ServiceBusException>(() =>
                messagingTopologyCreator.CreateTopology([new FakeQueueDescription("queue")], CancellationToken.None));

            Assert.Equal(ServiceBusFailureReason.QuotaExceeded, exception.Reason);
        }
    }

    public class CreateEventTopology
    {
        [Fact]
        public async Task WhenUsingEmulator_ThrowsInvalidOperationException()
        {
            var adminClient = new FakeServiceBusAdministrationClient();
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("localhost"), adminClient);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                messagingTopologyCreator.CreateEventTopology([new FakeEventDescription("topic")], CancellationToken.None));
        }

        [Fact]
        public async Task WhenTopicDoesNotExist_CreatesTopic()
        {
            var adminClient = new FakeServiceBusAdministrationClient { TopicExists = false };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            await messagingTopologyCreator.CreateEventTopology([new FakeEventDescription("topic")], CancellationToken.None);

            Assert.Equal(1, adminClient.CreateTopicCallCount);
        }

        [Fact]
        public async Task WhenTopicAlreadyExists_DoesNotCreateTopic()
        {
            var adminClient = new FakeServiceBusAdministrationClient { TopicExists = true };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            await messagingTopologyCreator.CreateEventTopology([new FakeEventDescription("topic")], CancellationToken.None);

            Assert.Equal(0, adminClient.CreateTopicCallCount);
        }

        [Fact]
        public async Task WhenConcurrentCreatorWinsTopicRace_SwallowsMessagingEntityAlreadyExists()
        {
            var adminClient = new FakeServiceBusAdministrationClient
            {
                TopicExists = false,
                ThrowOnCreateTopic = new ServiceBusException("already exists", ServiceBusFailureReason.MessagingEntityAlreadyExists)
            };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            await messagingTopologyCreator.CreateEventTopology([new FakeEventDescription("topic")], CancellationToken.None);

            Assert.Equal(1, adminClient.CreateTopicCallCount);
        }

        [Fact]
        public async Task WhenSubscriptionDoesNotExist_CreatesSubscriptionWithRule()
        {
            var adminClient = new FakeServiceBusAdministrationClient { TopicExists = true, SubscriptionExists = false };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            await messagingTopologyCreator.CreateEventTopology([new FakeEventSubscriptionDescription("topic", "subscription")], CancellationToken.None);

            Assert.Equal(1, adminClient.CreateSubscriptionCallCount);
            Assert.Equal(0, adminClient.DeleteRuleCallCount);
        }

        [Fact]
        public async Task WhenSubscriptionAlreadyExists_EnsuresFilterRuleInsteadOfCreatingSubscription()
        {
            var adminClient = new FakeServiceBusAdministrationClient { TopicExists = true, SubscriptionExists = true };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            await messagingTopologyCreator.CreateEventTopology([new FakeEventSubscriptionDescription("topic", "subscription")], CancellationToken.None);

            Assert.Equal(0, adminClient.CreateSubscriptionCallCount);
            Assert.Equal(1, adminClient.DeleteRuleCallCount);
            Assert.Equal(1, adminClient.CreateRuleCallCount);
        }

        [Fact]
        public async Task WhenConcurrentCreatorWinsSubscriptionRace_SwallowsMessagingEntityAlreadyExists()
        {
            var adminClient = new FakeServiceBusAdministrationClient
            {
                TopicExists = true,
                SubscriptionExists = false,
                ThrowOnCreateSubscription = new ServiceBusException("already exists", ServiceBusFailureReason.MessagingEntityAlreadyExists)
            };
            var messagingTopologyCreator = new MessagingTopologyCreator(CreateServiceBusClient("real.servicebus.windows.net"), adminClient);

            await messagingTopologyCreator.CreateEventTopology([new FakeEventSubscriptionDescription("topic", "subscription")], CancellationToken.None);

            Assert.Equal(1, adminClient.CreateSubscriptionCallCount);
        }
    }

    private static ServiceBusClient CreateServiceBusClient(string host)
    {
        return new ServiceBusClient($"Endpoint=sb://{host}/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOP1=");
    }

    private record FakeQueueDescription(string Name) : IQueueDescription
    {
        public TimeSpan DefaultMessageTimeToLive { get; } = TimeSpan.FromDays(1);
        public bool DeadLetterOnMessageExpiration { get; } = true;
        public TimeSpan LockDuration { get; } = TimeSpan.FromMinutes(1);
        public bool RequiresSession { get; }
    }

    private record FakeEventDescription(string TopicName) : IEventDescription
    {
        public TimeSpan? DefaultMessageTimeToLive { get; }
        public bool? DeadLetterOnMessageExpiration { get; }
    }

    private record FakeEventSubscriptionDescription(string TopicName, string SubscriptionName) : IEventSubscriptionDescription
    {
        public TimeSpan? DefaultMessageTimeToLive { get; }
        public bool? DeadLetterOnMessageExpiration { get; }
    }

    private class FakeServiceBusAdministrationClient()
        : ServiceBusAdministrationClient("Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOP1=")
    {
        public bool QueueExists { get; set; }
        public bool TopicExists { get; set; }
        public bool SubscriptionExists { get; set; }
        public Exception? ThrowOnCreateQueue { get; set; }
        public Exception? ThrowOnCreateTopic { get; set; }
        public Exception? ThrowOnCreateSubscription { get; set; }
        public Exception? ThrowOnCreateRule { get; set; }
        public int CreateQueueCallCount { get; private set; }
        public int CreateTopicCallCount { get; private set; }
        public int CreateSubscriptionCallCount { get; private set; }
        public int CreateRuleCallCount { get; private set; }
        public int DeleteRuleCallCount { get; private set; }

        public override Task<Response<bool>> QueueExistsAsync(string queueName, CancellationToken cancellationToken = default)
            => Task.FromResult(Response.FromValue(QueueExists, null!));

        public override Task<Response<QueueProperties>> CreateQueueAsync(CreateQueueOptions options, CancellationToken cancellationToken = default)
        {
            CreateQueueCallCount++;

            if (ThrowOnCreateQueue is not null)
                throw ThrowOnCreateQueue;

            return Task.FromResult(Response.FromValue<QueueProperties>(null!, null!));
        }

        public override Task<Response<bool>> TopicExistsAsync(string topicName, CancellationToken cancellationToken = default)
            => Task.FromResult(Response.FromValue(TopicExists, null!));

        public override Task<Response<TopicProperties>> CreateTopicAsync(CreateTopicOptions options, CancellationToken cancellationToken = default)
        {
            CreateTopicCallCount++;

            if (ThrowOnCreateTopic is not null)
                throw ThrowOnCreateTopic;

            return Task.FromResult(Response.FromValue<TopicProperties>(null!, null!));
        }

        public override Task<Response<bool>> SubscriptionExistsAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
            => Task.FromResult(Response.FromValue(SubscriptionExists, null!));

        public override Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(
            CreateSubscriptionOptions options,
            CreateRuleOptions ruleOptions,
            CancellationToken cancellationToken = default)
        {
            CreateSubscriptionCallCount++;

            if (ThrowOnCreateSubscription is not null)
                throw ThrowOnCreateSubscription;

            return Task.FromResult(Response.FromValue<SubscriptionProperties>(null!, null!));
        }

        public override Task<Response<RuleProperties>> CreateRuleAsync(
            string topicName,
            string subscriptionName,
            CreateRuleOptions options,
            CancellationToken cancellationToken = default)
        {
            CreateRuleCallCount++;

            if (ThrowOnCreateRule is not null)
                throw ThrowOnCreateRule;

            return Task.FromResult(Response.FromValue<RuleProperties>(null!, null!));
        }

        public override Task<Response> DeleteRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
        {
            DeleteRuleCallCount++;

            throw new ServiceBusException("not found", ServiceBusFailureReason.MessagingEntityNotFound);
        }
    }
}
