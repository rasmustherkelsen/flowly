using System.Diagnostics;
using Flowly.MessageInfrastructure.Callers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;

namespace Flowly.Tests.MessageInfrastructure.Callers;

public class CallSubmitterTests
{
    public class Submit
    {
        [Fact]
        public async Task StartsCallingWithCallQueueNameMessagingSystemMessageIdAndCorrelationId()
        {
            var instrumentation = new CapturingCallerInstrumentation();
            var registry = new ImmediateResolvingRegistry();
            var callSubmitter = CreateSubmitter(instrumentation, registry: registry);

            await callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None);

            Assert.Single(instrumentation.Started);
            Assert.Equal("ping", instrumentation.Started[0].CallQueueName);
            Assert.Equal("fake-bus", instrumentation.Started[0].MessagingSystem);
            Assert.True(Guid.TryParse(instrumentation.Started[0].MessageId, out _));
            Assert.True(Guid.TryParse(instrumentation.Started[0].CorrelationId, out _));
        }

        [Fact]
        public async Task RecordsSucceededAfterResponseReceived()
        {
            var instrumentation = new CapturingCallerInstrumentation();
            var registry = new ImmediateResolvingRegistry();
            var callSubmitter = CreateSubmitter(instrumentation, registry: registry);

            await callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None);

            Assert.Single(instrumentation.Succeeded);
            Assert.Equal("ping", instrumentation.Succeeded[0].CallQueueName);
            Assert.True(instrumentation.Succeeded[0].DurationMs >= 0);
            Assert.Empty(instrumentation.Failed);
        }

        [Fact]
        public async Task WhenSenderThrows_RecordsFailedAndRethrows()
        {
            var instrumentation = new CapturingCallerInstrumentation();
            var registry = new ImmediateResolvingRegistry();
            var callSubmitter = CreateSubmitter(instrumentation, sender: new ThrowingSender(new InvalidOperationException("boom")), registry: registry);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None));

            Assert.Equal("boom", ex.Message);
            Assert.Single(instrumentation.Failed);
            Assert.Equal("ping", instrumentation.Failed[0]);
            Assert.Empty(instrumentation.Succeeded);
        }

        [Fact]
        public async Task WhenResponseTimesOut_RecordsFailedAndRethrows()
        {
            var instrumentation = new CapturingCallerInstrumentation();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var pendingCallRegistry = new PendingCallRegistry<PongMessage>();
            var clientRegistry = new FakeClientRegistry(new FakeMessageBusClient(new CapturingSender()));
            var settings = new CallSubmitter<PingMessage, PongMessage>.Settings("ping", "ping-reply-sender", "stub", TimeSpan.FromSeconds(5));
            var callSubmitter = new CallSubmitter<PingMessage, PongMessage>(clientRegistry, settings, pendingCallRegistry, instrumentation);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callSubmitter.Submit(new PingMessage("hi"), cts.Token));

            Assert.Single(instrumentation.Failed);
        }

        [Fact]
        public async Task WhenMessageImplementsIOpenTelemetryTagsProvider_SetsTagsOnSpan()
        {
            var instrumentation = new ActivityCapturingCallerInstrumentation();
            var registry = new ImmediateResolvingTaggedRegistry();
            var settings = new CallSubmitter<TaggedPingMessage, PongMessage>.Settings("ping", "ping-reply-sender", "stub", TimeSpan.FromSeconds(5));
            var clientRegistry = new FakeClientRegistry(new FakeMessageBusClient(new CapturingSender()));
            var callSubmitter = new CallSubmitter<TaggedPingMessage, PongMessage>(clientRegistry, settings, registry, instrumentation);

            await callSubmitter.Submit(new TaggedPingMessage("order-42"), CancellationToken.None);

            Assert.Equal("order-42", instrumentation.CapturedActivity?.GetTagItem("order.id"));
        }

        [Fact]
        public async Task WhenMessageDoesNotImplementIOpenTelemetryTagsProvider_SetsNoCustomTags()
        {
            var instrumentation = new ActivityCapturingCallerInstrumentation();
            var registry = new ImmediateResolvingRegistry();
            var callSubmitter = CreateSubmitter(instrumentation, registry: registry);

            await callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None);

            Assert.Null(instrumentation.CapturedActivity?.GetTagItem("order.id"));
        }

        [Fact]
        public async Task SendsMessageWithCorrelationIdMatchingStartCallingArg()
        {
            var instrumentation = new CapturingCallerInstrumentation();
            var capturingSender = new CapturingSender();
            var callSubmitter = CreateSubmitter(instrumentation, sender: capturingSender);

            await callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None);

            var sentCorrelationId = capturingSender.LastProperties?.CorrelationId;
            Assert.Equal(instrumentation.Started[0].CorrelationId, sentCorrelationId);
        }

        [Fact]
        public async Task SendsMessageWithReplyToQueue()
        {
            var capturingSender = new CapturingSender();
            var callSubmitter = CreateSubmitter(new CapturingCallerInstrumentation(), sender: capturingSender);

            await callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None);

            Assert.Equal("ping-reply-sender", capturingSender.LastProperties?.ReplyTo);
        }
    }

    private static CallSubmitter<PingMessage, PongMessage> CreateSubmitter(
        ICallerInstrumentation instrumentation,
        IMessageBusSender? sender = null,
        ImmediateResolvingRegistry? registry = null)
    {
        var actualSender = sender ?? new CapturingSender();
        var actualRegistry = registry ?? new ImmediateResolvingRegistry();
        var clientRegistry = new FakeClientRegistry(new FakeMessageBusClient(actualSender));
        var settings = new CallSubmitter<PingMessage, PongMessage>.Settings("ping", "ping-reply-sender", "stub", TimeSpan.FromSeconds(5));

        return new CallSubmitter<PingMessage, PongMessage>(clientRegistry, settings, actualRegistry, instrumentation);
    }

    private record PingMessage(string Payload) : IReturns<PongMessage>;

    private record TaggedPingMessage(string OrderId) : IReturns<PongMessage>, IOpenTelemetryTagsProvider
    {
        public IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags() =>
            [new("order.id", OrderId)];
    }

    private record PongMessage(string Echo);

    private sealed class ImmediateResolvingRegistry : IPendingCallRegistry<PongMessage>
    {
        public Task<PongMessage> Register(string correlationId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PongMessage("pong"));
        }

        public bool TryResolve(string correlationId, PongMessage response) => true;
    }

    private sealed class ImmediateResolvingTaggedRegistry : IPendingCallRegistry<PongMessage>
    {
        public Task<PongMessage> Register(string correlationId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PongMessage("pong"));
        }

        public bool TryResolve(string correlationId, PongMessage response) => true;
    }

    private sealed class CapturingCallerInstrumentation : ICallerInstrumentation
    {
        public List<(string CallQueueName, string MessagingSystem, string MessageId, string CorrelationId)> Started { get; } = [];
        public List<(string CallQueueName, double DurationMs)> Succeeded { get; } = [];
        public List<string> Failed { get; } = [];

        public Activity? StartCalling(string callQueueName, string messagingSystem, string messageId, string correlationId)
        {
            Started.Add((callQueueName, messagingSystem, messageId, correlationId));
            return null;
        }

        public void RecordSucceeded(string callQueueName, double durationMs) => Succeeded.Add((callQueueName, durationMs));

        public void RecordFailed(string callQueueName) => Failed.Add(callQueueName);

        public Activity? StartReceivingResponse(string replyQueueName, string messagingSystem, ActivityContext parentContext, string messageId, string correlationId) => null;
    }

    private sealed class ActivityCapturingCallerInstrumentation : ICallerInstrumentation
    {
        public Activity? CapturedActivity { get; private set; }

        public Activity? StartCalling(string callQueueName, string messagingSystem, string messageId, string correlationId)
        {
            CapturedActivity = new Activity("flowly.call test").Start();
            return CapturedActivity;
        }

        public void RecordSucceeded(string callQueueName, double durationMs) { }

        public void RecordFailed(string callQueueName) { }

        public Activity? StartReceivingResponse(string replyQueueName, string messagingSystem, ActivityContext parentContext, string messageId, string correlationId) => null;
    }

    private sealed class CapturingSender : IMessageBusSender
    {
        public MessageProperties? LastProperties { get; private set; }

        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            LastProperties = messageProperties;
            return Task.CompletedTask;
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingSender(Exception exception) : IMessageBusSender
    {
        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default) =>
            Task.FromException(exception);

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMessageBusClient(IMessageBusSender sender) : IMessageBusClient
    {
        public string MessagingSystem => "fake-bus";

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => Task.FromResult(sender);

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotSupportedException();

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotSupportedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotSupportedException();

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => Task.FromResult(0L);
    }

    private sealed class FakeClientRegistry(IMessageBusClient client) : IMessageBusClientRegistry
    {
        public string PrimaryProviderName => "stub";

        public IMessageBusClient GetClient(string providerName) => client;

        public bool IsRegistered(string providerName) => true;

        public IReadOnlyList<RegisteredTransport> GetAll() => [new RegisteredTransport("stub", true, null)];

        public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride) { }
    }
}
