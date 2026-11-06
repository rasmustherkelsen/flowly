using System.Diagnostics;
using Flowly.MessageInfrastructure.Callers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.SendSteps;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

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
            var callSubmitter = new CallSubmitter<PingMessage, PongMessage>(clientRegistry, "ping", "ping-reply-sender", "stub", TimeSpan.FromSeconds(5), pendingCallRegistry, instrumentation, SendStepRunner.None);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callSubmitter.Submit(new PingMessage("hi"), cts.Token));

            Assert.Single(instrumentation.Failed);
        }

        [Fact]
        public async Task WhenMessageImplementsIOpenTelemetryTagsProvider_SetsTagsOnSpan()
        {
            var instrumentation = new ActivityCapturingCallerInstrumentation();
            var registry = new ImmediateResolvingTaggedRegistry();
            var clientRegistry = new FakeClientRegistry(new FakeMessageBusClient(new CapturingSender()));
            var callSubmitter = new CallSubmitter<TaggedPingMessage, PongMessage>(clientRegistry, "ping", "ping-reply-sender", "stub", TimeSpan.FromSeconds(5), registry, instrumentation, SendStepRunner.None);

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

    public class WithSendSteps
    {
        [Fact]
        public async Task StepRunsBeforeAnythingIsSentOrRegistered()
        {
            var observations = new List<string>();
            var sender = new CapturingSender();
            var registry = new RecordingRegistry();
            var instrumentation = new CapturingCallerInstrumentation();
            var services = new ServiceCollection();
            services.AddSingleton(observations);
            services.AddSingleton<Func<string>>(() => $"sent:{sender.Sent},registered:{registry.Registered.Count},started:{instrumentation.Started.Count}");
            services.AddScoped<ObservingStep>();
            using var serviceProvider = services.BuildServiceProvider();
            var callSubmitter = CreateSubmitter(serviceProvider, instrumentation, sender, registry, typeof(ObservingStep));

            await callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None);

            Assert.Equal(["before:sent:0,registered:0,started:0", "after:sent:1,registered:1,started:1"], observations);
        }

        [Fact]
        public async Task StepThatThrows_SendsNothingRegistersNoPendingCallAndRecordsNoMetrics()
        {
            var sender = new CapturingSender();
            var registry = new RecordingRegistry();
            var instrumentation = new CapturingCallerInstrumentation();
            using var serviceProvider = CreateServiceProvider(typeof(RejectingStep));
            var callSubmitter = CreateSubmitter(serviceProvider, instrumentation, sender, registry, typeof(RejectingStep));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None));

            Assert.Equal(0, sender.Sent);
            Assert.Empty(registry.Registered);
            Assert.Empty(instrumentation.Started);
            Assert.Empty(instrumentation.Succeeded);
            Assert.Empty(instrumentation.Failed);
        }

        [Fact]
        public async Task StepThatThrows_ExceptionReachesCallerUnchanged()
        {
            using var serviceProvider = CreateServiceProvider(typeof(RejectingStep));
            var callSubmitter = CreateSubmitter(serviceProvider, new CapturingCallerInstrumentation(), new CapturingSender(), new RecordingRegistry(), typeof(RejectingStep));

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None));

            Assert.Equal("rejected", exception.Message);
        }

        [Fact]
        public async Task StepThatSkipsNext_ThrowsInvalidOperationExceptionAndSendsNothing()
        {
            var sender = new CapturingSender();
            var registry = new RecordingRegistry();
            var instrumentation = new CapturingCallerInstrumentation();
            using var serviceProvider = CreateServiceProvider(typeof(SkippingStep));
            var callSubmitter = CreateSubmitter(serviceProvider, instrumentation, sender, registry, typeof(SkippingStep));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None));

            Assert.Contains("send step", exception.Message);
            Assert.Contains(nameof(PingMessage), exception.Message);
            Assert.Equal(0, sender.Sent);
            Assert.Empty(registry.Registered);
            Assert.Empty(instrumentation.Failed);
        }

        [Fact]
        public async Task StepPassingThrough_ReturnsReplyUnchangedAndRecordsSucceeded()
        {
            var registry = new RecordingRegistry();
            var instrumentation = new CapturingCallerInstrumentation();
            using var serviceProvider = CreateServiceProvider(typeof(PassThroughStep));
            var callSubmitter = CreateSubmitter(serviceProvider, instrumentation, new CapturingSender(), registry, typeof(PassThroughStep));

            var response = await callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None);

            Assert.Same(registry.Reply, response);
            Assert.Single(instrumentation.Succeeded);
            Assert.Empty(instrumentation.Failed);
        }

        [Fact]
        public async Task StepPassingThrough_WhenSenderThrows_RecordsFailed()
        {
            var instrumentation = new CapturingCallerInstrumentation();
            using var serviceProvider = CreateServiceProvider(typeof(PassThroughStep));
            var callSubmitter = CreateSubmitter(serviceProvider, instrumentation, new ThrowingSender(new InvalidOperationException("boom")), new RecordingRegistry(), typeof(PassThroughStep));

            await Assert.ThrowsAsync<InvalidOperationException>(() => callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None));

            Assert.Single(instrumentation.Failed);
        }

        [Fact]
        public async Task PassesMessageAndCancellationTokenToTheStep()
        {
            var contexts = new List<SendStepContext<PingMessage>>();
            var services = new ServiceCollection();
            services.AddSingleton(contexts);
            services.AddScoped<ContextCapturingStep>();
            using var serviceProvider = services.BuildServiceProvider();
            var callSubmitter = CreateSubmitter(serviceProvider, new CapturingCallerInstrumentation(), new CapturingSender(), new RecordingRegistry(), typeof(ContextCapturingStep));
            var message = new PingMessage("hi");
            using var cancellationTokenSource = new CancellationTokenSource();

            await callSubmitter.Submit(message, cancellationTokenSource.Token);

            var sendStepContext = Assert.Single(contexts);
            Assert.Same(message, sendStepContext.Message);
            Assert.Equal(cancellationTokenSource.Token, sendStepContext.CancellationToken);
        }

        [Fact]
        public async Task StepForAnotherMessageType_DoesNotRun()
        {
            var sender = new CapturingSender();
            using var serviceProvider = CreateServiceProvider(typeof(OtherMessageStep));
            var callSubmitter = CreateSubmitter(serviceProvider, new CapturingCallerInstrumentation(), sender, new RecordingRegistry(), typeof(OtherMessageStep));

            await callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None);

            Assert.Equal(1, sender.Sent);
        }

        [Fact]
        public async Task WithoutRegisteredSteps_CreatesNoScope()
        {
            var sender = new CapturingSender();
            var clientRegistry = new FakeClientRegistry(new FakeMessageBusClient(sender));
            var callSubmitter = new CallSubmitter<PingMessage, PongMessage>(
                clientRegistry,
                "ping",
                "ping-reply-sender",
                "stub",
                TimeSpan.FromSeconds(5),
                new RecordingRegistry(),
                new CapturingCallerInstrumentation(),
                new SendStepRunner([], new ThrowingServiceScopeFactory()));

            await callSubmitter.Submit(new PingMessage("hi"), CancellationToken.None);

            Assert.Equal(1, sender.Sent);
        }

        private static ServiceProvider CreateServiceProvider(params Type[] stepTypes)
        {
            var services = new ServiceCollection();

            foreach (var stepType in stepTypes)
            {
                services.AddScoped(stepType);
            }

            return services.BuildServiceProvider();
        }

        private static CallSubmitter<PingMessage, PongMessage> CreateSubmitter(
            IServiceProvider serviceProvider,
            ICallerInstrumentation instrumentation,
            IMessageBusSender sender,
            RecordingRegistry registry,
            params Type[] stepTypes)
        {
            var sendStepRunner = new SendStepRunner(
                stepTypes.Select(stepType => new SendStepRegistration(stepType, SendStepRegistration.ResolveFilterType(stepType))),
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            return new CallSubmitter<PingMessage, PongMessage>(
                new FakeClientRegistry(new FakeMessageBusClient(sender)),
                "ping",
                "ping-reply-sender",
                "stub",
                TimeSpan.FromSeconds(5),
                registry,
                instrumentation,
                sendStepRunner);
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
        return new CallSubmitter<PingMessage, PongMessage>(clientRegistry, "ping", "ping-reply-sender", "stub", TimeSpan.FromSeconds(5), actualRegistry, instrumentation, SendStepRunner.None);
    }

    private record PingMessage(string Payload) : IReturns<PongMessage>;

    private record TaggedPingMessage(string OrderId) : IReturns<PongMessage>, IOpenTelemetryTagsProvider
    {
        public IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags() =>
            [new("order.id", OrderId)];
    }

    private record PongMessage(string Echo);

    private sealed class RecordingRegistry : IPendingCallRegistry<PongMessage>
    {
        public PongMessage Reply { get; } = new("pong");
        public List<string> Registered { get; } = [];

        public Task<PongMessage> Register(string correlationId, CancellationToken cancellationToken)
        {
            Registered.Add(correlationId);
            return Task.FromResult(Reply);
        }

        public bool TryResolve(string correlationId, PongMessage response) => true;
    }

    private sealed class ThrowingServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("no scope expected");
    }

    private sealed class ObservingStep(List<string> observations, Func<string> probe) : SendStep
    {
        public override async Task Handle(SendStepContext<object> context, SendStepDelegate next)
        {
            observations.Add($"before:{probe()}");
            await next();
            observations.Add($"after:{probe()}");
        }
    }

    private sealed class RejectingStep : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next)
            => throw new UnauthorizedAccessException("rejected");
    }

    private sealed class SkippingStep : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next) => Task.CompletedTask;
    }

    private sealed class PassThroughStep : SendStep;

    private sealed class OtherMessageStep : SendStep<PongMessage>
    {
        public override Task Handle(SendStepContext<PongMessage> context, SendStepDelegate next) => throw new UnauthorizedAccessException("rejected");
    }

    private sealed class ContextCapturingStep(List<SendStepContext<PingMessage>> contexts) : SendStep<PingMessage>
    {
        public override Task Handle(SendStepContext<PingMessage> context, SendStepDelegate next)
        {
            contexts.Add(context);

            return next();
        }
    }

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
        public int Sent { get; private set; }

        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            LastProperties = messageProperties;
            Sent++;
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
