using System.Diagnostics;
using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.Tests.MessageInfrastructure.Events;

public class EventSubmitterTests
{
    public class Raise
    {
        [Fact]
        public async Task PublishesEventToTopicNamedInSettings()
        {
            var publisher = new CapturingEventPublisher();
            var client = new FakeEventCapableClient(publisher);
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubPublisherInstrumentation();
            var topicSettings = new EventSubmitter<OrderPlaced>.TopicSettings("order-placed", "primary");
            var eventSubmitter = new EventSubmitter<OrderPlaced>(registry, topicSettings, instrumentation);

            await eventSubmitter.Raise(new OrderPlaced("order-1"));

            Assert.Equal("order-placed", client.CreatedPublisherForTopic);
            Assert.Single(publisher.SentMessages);
            Assert.Equal("order-1", ((OrderPlaced)publisher.SentMessages[0].Message).OrderId);
        }

        [Fact]
        public async Task SentMessageHasGeneratedMessageIdAndEmptyCorrelationId()
        {
            var publisher = new CapturingEventPublisher();
            var registry = new SingleClientRegistry(new FakeEventCapableClient(publisher), "primary");
            var instrumentation = new StubPublisherInstrumentation();
            var topicSettings = new EventSubmitter<OrderPlaced>.TopicSettings("order-placed", "primary");
            var eventSubmitter = new EventSubmitter<OrderPlaced>(registry, topicSettings, instrumentation);

            await eventSubmitter.Raise(new OrderPlaced("x"));

            var properties = publisher.SentMessages[0].Properties;
            Assert.True(Guid.TryParse(properties.MessageId, out _));
            Assert.Equal(string.Empty, properties.CorrelationId);
        }

        [Fact]
        public async Task WhenClientIsNotEventCapable_ThrowsInvalidOperationException()
        {
            var registry = new SingleClientRegistry(new NonEventCapableClient(), "primary");
            var instrumentation = new StubPublisherInstrumentation();
            var topicSettings = new EventSubmitter<OrderPlaced>.TopicSettings("order-placed", "primary");
            var eventSubmitter = new EventSubmitter<OrderPlaced>(registry, topicSettings, instrumentation);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => eventSubmitter.Raise(new OrderPlaced("x")));

            Assert.Contains("primary", exception.Message);
            Assert.Contains(nameof(IEventCapableMessageBusClient), exception.Message);
        }

        [Fact]
        public async Task SuccessfulPublish_RecordsRaisedOnInstrumentation()
        {
            var publisher = new CapturingEventPublisher();
            var registry = new SingleClientRegistry(new FakeEventCapableClient(publisher), "primary");
            var instrumentation = new StubPublisherInstrumentation();
            var topicSettings = new EventSubmitter<OrderPlaced>.TopicSettings("order-placed", "primary");
            var eventSubmitter = new EventSubmitter<OrderPlaced>(registry, topicSettings, instrumentation);

            await eventSubmitter.Raise(new OrderPlaced("x"));

            Assert.Single(instrumentation.Raised);
            Assert.Equal("order-placed", instrumentation.Raised[0].TopicName);
            Assert.Empty(instrumentation.Failed);
        }

        [Fact]
        public async Task WhenPublisherThrows_RecordsFailedAndRethrows()
        {
            var publisher = new ThrowingEventPublisher(new InvalidOperationException("boom"));
            var registry = new SingleClientRegistry(new FakeEventCapableClient(publisher), "primary");
            var instrumentation = new StubPublisherInstrumentation();
            var topicSettings = new EventSubmitter<OrderPlaced>.TopicSettings("order-placed", "primary");
            var eventSubmitter = new EventSubmitter<OrderPlaced>(registry, topicSettings, instrumentation);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => eventSubmitter.Raise(new OrderPlaced("x")));

            Assert.Equal("boom", exception.Message);
            Assert.Single(instrumentation.Failed);
            Assert.Equal("order-placed", instrumentation.Failed[0]);
            Assert.Empty(instrumentation.Raised);
        }

        [Fact]
        public async Task StartsRaisingActivityWithMessagingSystemAndMessageId()
        {
            var publisher = new CapturingEventPublisher();
            var client = new FakeEventCapableClient(publisher) { MessagingSystem = "fake-bus" };
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubPublisherInstrumentation();
            var topicSettings = new EventSubmitter<OrderPlaced>.TopicSettings("order-placed", "primary");
            var eventSubmitter = new EventSubmitter<OrderPlaced>(registry, topicSettings, instrumentation);

            await eventSubmitter.Raise(new OrderPlaced("x"));

            var startedMessageId = publisher.SentMessages[0].Properties.MessageId;
            Assert.Single(instrumentation.Started);
            Assert.Equal("order-placed", instrumentation.Started[0].TopicName);
            Assert.Equal("fake-bus", instrumentation.Started[0].MessagingSystem);
            Assert.Equal(startedMessageId, instrumentation.Started[0].MessageId);
        }

        [Fact]
        public async Task PassesCancellationTokenThroughToPublisher()
        {
            var publisher = new CapturingEventPublisher();
            var registry = new SingleClientRegistry(new FakeEventCapableClient(publisher), "primary");
            var instrumentation = new StubPublisherInstrumentation();
            var topicSettings = new EventSubmitter<OrderPlaced>.TopicSettings("order-placed", "primary");
            var eventSubmitter = new EventSubmitter<OrderPlaced>(registry, topicSettings, instrumentation);
            using var cancellationTokenSource = new CancellationTokenSource();

            await eventSubmitter.Raise(new OrderPlaced("x"), cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, publisher.ReceivedTokens.Single());
        }

        [Fact]
        public async Task UsesProviderNameFromTopicSettingsToResolveClient()
        {
            var secondaryPublisher = new CapturingEventPublisher();
            var secondaryClient = new FakeEventCapableClient(secondaryPublisher);
            var registry = new TwoClientRegistry(
                "primary",
                new FakeEventCapableClient(new CapturingEventPublisher()),
                "secondary",
                secondaryClient);
            var instrumentation = new StubPublisherInstrumentation();
            var topicSettings = new EventSubmitter<OrderPlaced>.TopicSettings("order-placed", "secondary");
            var eventSubmitter = new EventSubmitter<OrderPlaced>(registry, topicSettings, instrumentation);

            await eventSubmitter.Raise(new OrderPlaced("x"));

            Assert.Single(secondaryPublisher.SentMessages);
        }
    }

    private record OrderPlaced(string OrderId);

    private class SingleClientRegistry(IMessageBusClient client, string primaryProviderName) : IMessageBusClientRegistry
    {
        public string PrimaryProviderName { get; } = primaryProviderName;

        public IMessageBusClient GetClient(string providerName)
        {
            return client;
        }

        public bool IsRegistered(string providerName)
        {
            return true;
        }

        public IReadOnlyList<RegisteredTransport> GetAll()
        {
            return [new RegisteredTransport(PrimaryProviderName, true, null)];
        }

        public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride)
        {
        }
    }

    private class TwoClientRegistry(
        string primaryName,
        IMessageBusClient primaryClient,
        string secondaryName,
        IMessageBusClient secondaryClient) : IMessageBusClientRegistry
    {
        public string PrimaryProviderName => primaryName;

        public IMessageBusClient GetClient(string providerName)
        {
            return providerName == primaryName ? primaryClient : secondaryClient;
        }

        public bool IsRegistered(string providerName)
        {
            return providerName == primaryName || providerName == secondaryName;
        }

        public IReadOnlyList<RegisteredTransport> GetAll()
        {
            return
            [
                new RegisteredTransport(primaryName, true, null),
                new RegisteredTransport(secondaryName, false, null)
            ];
        }

        public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride)
        {
        }
    }

    private class FakeEventCapableClient(IMessageBusSender publisher) : IMessageBusClient, IEventCapableMessageBusClient
    {
        public string? CreatedPublisherForTopic { get; private set; }

        public Task<IMessageBusSender> CreateEventPublisher(string topicName)
        {
            CreatedPublisherForTopic = topicName;
            return Task.FromResult(publisher);
        }

        public Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(string topicName, string subscriptionName, MessageBusProcessorOptions options)
        {
            throw new NotSupportedException();
        }

        public Task<IMessageBusSender> CreateEventRetrySender(string topicName, string subscriptionName)
        {
            throw new NotSupportedException();
        }

        public Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicName, string subscriptionName)
        {
            throw new NotSupportedException();
        }

        public Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public string MessagingSystem { get; set; } = "fake";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            throw new NotSupportedException();
        }

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        {
            throw new NotSupportedException();
        }

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private class NonEventCapableClient : IMessageBusClient
    {
        public string MessagingSystem => "fake";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            throw new NotSupportedException();
        }

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        {
            throw new NotSupportedException();
        }

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private class CapturingEventPublisher : IMessageBusSender
    {
        public List<(object Message, MessageProperties Properties)> SentMessages { get; } = [];
        public List<CancellationToken> ReceivedTokens { get; } = [];

        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            SentMessages.Add((message!, messageProperties));
            ReceivedTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private class ThrowingEventPublisher(Exception exception) : IMessageBusSender
    {
        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            return Task.FromException(exception);
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private class StubPublisherInstrumentation : IEventPublisherInstrumentation
    {
        public List<(string TopicName, string MessagingSystem, string MessageId)> Started { get; } = [];
        public List<(string TopicName, double DurationMs)> Raised { get; } = [];
        public List<string> Failed { get; } = [];

        public bool IsEnabled => true;

        public Activity? StartRaising(string topicName, string messagingSystem, string messageId)
        {
            Started.Add((topicName, messagingSystem, messageId));
            return null;
        }

        public void RecordRaised(string topicName, double durationMs)
        {
            Raised.Add((topicName, durationMs));
        }

        public void RecordFailed(string topicName)
        {
            Failed.Add(topicName);
        }
    }
}