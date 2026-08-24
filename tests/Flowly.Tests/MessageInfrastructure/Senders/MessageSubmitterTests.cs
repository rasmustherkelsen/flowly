using System.Diagnostics;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;

namespace Flowly.Tests.MessageInfrastructure.Senders;

public class MessageSubmitterTests
{
    public class Submit
    {
        [Fact]
        public async Task CreatesSenderForConfiguredQueueName()
        {
            var capturingSender = new CapturingSender();
            var client = new FakeMessageBusClient(capturingSender);
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var queueSettings = new MessageSubmitter<OrderPlaced>.QueueSettings("order-placed", "primary");
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, queueSettings, instrumentation);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Equal("order-placed", client.CreatedSenderForQueue);
        }

        [Fact]
        public async Task SendsMessageWithGeneratedMessageIdAndEmptyCorrelationId()
        {
            var capturingSender = new CapturingSender();
            var client = new FakeMessageBusClient(capturingSender);
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var queueSettings = new MessageSubmitter<OrderPlaced>.QueueSettings("order-placed", "primary");
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, queueSettings, instrumentation);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            var properties = capturingSender.SentMessages[0].Properties;
            Assert.True(Guid.TryParse(properties.MessageId, out _));
            Assert.Equal(string.Empty, properties.CorrelationId);
        }

        [Fact]
        public async Task SuccessfulSubmit_RecordsSentOnInstrumentation()
        {
            var client = new FakeMessageBusClient(new CapturingSender());
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var queueSettings = new MessageSubmitter<OrderPlaced>.QueueSettings("order-placed", "primary");
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, queueSettings, instrumentation);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Single(instrumentation.Sent);
            Assert.Equal("order-placed", instrumentation.Sent[0].QueueName);
            Assert.Empty(instrumentation.Failed);
        }

        [Fact]
        public async Task WhenSenderThrows_RecordsFailedAndRethrows()
        {
            var client = new FakeMessageBusClient(new ThrowingSender(new InvalidOperationException("boom")));
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var queueSettings = new MessageSubmitter<OrderPlaced>.QueueSettings("order-placed", "primary");
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, queueSettings, instrumentation);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None));

            Assert.Equal("boom", exception.Message);
            Assert.Single(instrumentation.Failed);
            Assert.Equal("order-placed", instrumentation.Failed[0]);
            Assert.Empty(instrumentation.Sent);
        }

        [Fact]
        public async Task StartsSendingActivityWithMessagingSystemAndMessageId()
        {
            var capturingSender = new CapturingSender();
            var client = new FakeMessageBusClient(capturingSender) { MessagingSystem = "fake-bus" };
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var queueSettings = new MessageSubmitter<OrderPlaced>.QueueSettings("order-placed", "primary");
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, queueSettings, instrumentation);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            var sentMessageId = capturingSender.SentMessages[0].Properties.MessageId;
            Assert.Single(instrumentation.Started);
            Assert.Equal("order-placed", instrumentation.Started[0].QueueName);
            Assert.Equal("fake-bus", instrumentation.Started[0].MessagingSystem);
            Assert.Equal(sentMessageId, instrumentation.Started[0].MessageId);
        }

        [Fact]
        public async Task PassesCancellationTokenThroughToSender()
        {
            var capturingSender = new CapturingSender();
            var client = new FakeMessageBusClient(capturingSender);
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var queueSettings = new MessageSubmitter<OrderPlaced>.QueueSettings("order-placed", "primary");
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, queueSettings, instrumentation);
            using var cancellationTokenSource = new CancellationTokenSource();

            await messageSubmitter.Submit(new OrderPlaced("x"), cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, capturingSender.ReceivedTokens.Single());
        }

        [Fact]
        public async Task PassesPartitionKeyThroughToSentMessageProperties()
        {
            var capturingSender = new CapturingSender();
            var client = new FakeMessageBusClient(capturingSender);
            var registry = new SingleClientRegistry(client, "primary");
            var instrumentation = new StubSubmitterInstrumentation();
            var queueSettings = new MessageSubmitter<OrderPlaced>.QueueSettings("order-placed", "primary");
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, queueSettings, instrumentation);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None, partitionKey: "customer-7");

            Assert.Equal("customer-7", capturingSender.SentMessages[0].Properties.PartitionKey);
        }
    }

    public class WhenMessageImplementsIOpenTelemetryTagsProvider
    {
        [Fact]
        public async Task SetsTagsOnSpan()
        {
            var instrumentation = new ActivityReturningStubInstrumentation();
            var client = new FakeMessageBusClient(new CapturingSender());
            var registry = new SingleClientRegistry(client, "primary");
            var queueSettings = new MessageSubmitter<TaggedOrder>.QueueSettings("orders", "primary");
            var messageSubmitter = new MessageSubmitter<TaggedOrder>(registry, queueSettings, instrumentation);

            await messageSubmitter.Submit(new TaggedOrder("order-123"), CancellationToken.None);

            Assert.Equal("order-123", instrumentation.Activity.GetTagItem("order.id"));
        }

        [Fact]
        public async Task WhenMessageDoesNotImplementInterface_SetsNoCustomTags()
        {
            var instrumentation = new ActivityReturningStubInstrumentation();
            var client = new FakeMessageBusClient(new CapturingSender());
            var registry = new SingleClientRegistry(client, "primary");
            var queueSettings = new MessageSubmitter<OrderPlaced>.QueueSettings("order-placed", "primary");
            var messageSubmitter = new MessageSubmitter<OrderPlaced>(registry, queueSettings, instrumentation);

            await messageSubmitter.Submit(new OrderPlaced("x"), CancellationToken.None);

            Assert.Null(instrumentation.Activity.GetTagItem("order.id"));
        }
    }

    private record OrderPlaced(string OrderId);

    private record TaggedOrder(string OrderId) : IOpenTelemetryTagsProvider
    {
        public IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags() =>
            [new("order.id", OrderId)];
    }

    private sealed class SingleClientRegistry(IMessageBusClient client, string primary) : IMessageBusClientRegistry
    {
        public string PrimaryProviderName { get; } = primary;

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

        public void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride)
        {
        }
    }

    private sealed class FakeMessageBusClient(IMessageBusSender sender) : IMessageBusClient
    {
        public string? CreatedSenderForQueue { get; private set; }
        public string MessagingSystem { get; set; } = "fake";

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        {
            CreatedSenderForQueue = queueName;
            return Task.FromResult(sender);
        }

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

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class CapturingSender : IMessageBusSender
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
            return Task.CompletedTask;
        }

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSender(Exception exception) : IMessageBusSender
    {
        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            return Task.FromException(exception);
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ActivityReturningStubInstrumentation : ISubmitterInstrumentation
    {
        public Activity Activity { get; } = new Activity("flowly.send test").Start();

        public bool IsEnabled => true;

        public Activity? StartSending(string queueName, string messagingSystem, string messageId) => Activity;

        public void RecordSent(string queueName, double durationMs) { }

        public void RecordFailed(string queueName) { }
    }

    private sealed class StubSubmitterInstrumentation : ISubmitterInstrumentation
    {
        public List<(string QueueName, string MessagingSystem, string MessageId)> Started { get; } = [];
        public List<(string QueueName, double DurationMs)> Sent { get; } = [];
        public List<string> Failed { get; } = [];

        public bool IsEnabled => true;

        public Activity? StartSending(string queueName, string messagingSystem, string messageId)
        {
            Started.Add((queueName, messagingSystem, messageId));
            return null;
        }

        public void RecordSent(string queueName, double durationMs)
        {
            Sent.Add((queueName, durationMs));
        }

        public void RecordFailed(string queueName)
        {
            Failed.Add(queueName);
        }
    }
}