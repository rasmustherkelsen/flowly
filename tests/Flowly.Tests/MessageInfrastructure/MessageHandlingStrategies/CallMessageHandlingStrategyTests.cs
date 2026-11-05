using Flowly.MessageInfrastructure.HandlerSteps;
using Flowly.MessageInfrastructure.StreamPartitions;
using System.Diagnostics;
using Flowly.MessageInfrastructure.MessageHandlingStrategies;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.Tests.MessageInfrastructure.MessageHandlingStrategies;

public class CallMessageHandlingStrategyTests
{
    public class HandleMessage
    {
        [Fact]
        public async Task SendsHandlerResponseToReplyToQueue()
        {
            var sender = new CapturingSender();
            var (strategy, handler) = CreateStrategy(sender);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("test-payload"),
                new MessageProperties("msg-1", "corr-1", ReplyTo: "reply.sender"));

            var services = new ServiceCollection();
            services.AddScoped<CallHandler<PingMessage, PongMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            await strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.NotNull(sender.SentMessage);
            Assert.IsType<PongMessage>(sender.SentMessage);
            Assert.Equal("echo: test-payload", ((PongMessage)sender.SentMessage!).Echo);
        }

        [Fact]
        public async Task PreservesCorrelationIdInResponse()
        {
            var sender = new CapturingSender();
            var (strategy, handler) = CreateStrategy(sender);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("payload"),
                new MessageProperties("msg-1", "my-correlation-id", ReplyTo: "reply.sender"));

            var services = new ServiceCollection();
            services.AddScoped<CallHandler<PingMessage, PongMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            await strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.Equal("my-correlation-id", sender.SentProperties?.CorrelationId);
        }

        [Fact]
        public async Task SendsToTheReplyToQueueFromMessageProperties()
        {
            var sender = new CapturingSender();
            var fakeSenderFactory = new FakeSenderFactory("reply.sender", sender);
            var (strategy, handler) = CreateStrategy(sender, fakeSenderFactory);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("payload"),
                new MessageProperties("msg-1", "corr-1", ReplyTo: "reply.sender"));

            var services = new ServiceCollection();
            services.AddScoped<CallHandler<PingMessage, PongMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            await strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.Equal("reply.sender", fakeSenderFactory.RequestedQueueName);
        }

        [Fact]
        public async Task WithMissingReplyTo_Throws()
        {
            var sender = new CapturingSender();
            var (strategy, _) = CreateStrategy(sender);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("payload"),
                new MessageProperties("msg-1", "corr-1"));

            var services = new ServiceCollection();
            using var scope = services.BuildServiceProvider().CreateScope();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None));
        }
    }

    public class HandleMessageWithHandlerSteps
    {
        [Fact]
        public async Task RunsStepAroundHandlerAndSendsReplyAfterStep()
        {
            var log = new List<string>();
            var sender = new LoggingSender(log);
            var (strategy, _) = CreateStrategy(sender, runner: CreateRunner(typeof(LoggingStep)));
            using var scope = CreateScope(log, typeof(LoggingStep));

            await strategy.HandleMessage(CreateReceivedMessage(), scope.ServiceProvider, CancellationToken.None);

            Assert.Equal(["step:before", "handler", "step:after", "reply"], log);
        }

        [Fact]
        public async Task PassesRequestHandlerTypeAndPropertiesToStep()
        {
            var log = new List<string>();
            var (strategy, _) = CreateStrategy(new CapturingSender(), runner: CreateRunner(typeof(ContextLoggingStep)));
            using var scope = CreateScope(log, typeof(ContextLoggingStep));

            await strategy.HandleMessage(CreateReceivedMessage("request-1"), scope.ServiceProvider, CancellationToken.None);

            Assert.Equal([$"step:{nameof(LoggingEchoCallHandler)}:request-1:msg-1", "handler"], log);
        }

        [Fact]
        public async Task StepAndHandlerShareScopedService()
        {
            var log = new List<string>();
            var (strategy, _) = CreateStrategy(new CapturingSender(), runner: CreateRunner(typeof(ScopedStateStep)));
            using var scope = CreateScope(log, typeof(ScopedStateStep));

            await strategy.HandleMessage(CreateReceivedMessage(), scope.ServiceProvider, CancellationToken.None);

            Assert.Equal(["handler:state=from-step"], log);
        }

        [Fact]
        public async Task WithStepNotCallingNext_SendsNoReplyAndDoesNotInvokeHandler()
        {
            var log = new List<string>();
            var sender = new CapturingSender();
            var (strategy, _) = CreateStrategy(sender, runner: CreateRunner(typeof(SkippingStep)));
            using var scope = CreateScope(log, typeof(SkippingStep));

            await strategy.HandleMessage(CreateReceivedMessage(), scope.ServiceProvider, CancellationToken.None);

            Assert.Null(sender.SentMessage);
            Assert.Equal(["step:skipped"], log);
        }

        [Fact]
        public async Task WithStepNotCallingNext_LogsWarning()
        {
            var logger = new FakeTypedLogger<CallMessageHandlingStrategy<PingMessage, PongMessage>>();
            var (strategy, _) = CreateStrategy(new CapturingSender(), runner: CreateRunner(typeof(SkippingStep)), logger: logger);
            using var scope = CreateScope([], typeof(SkippingStep));

            await strategy.HandleMessage(CreateReceivedMessage(), scope.ServiceProvider, CancellationToken.None);

            var entry = Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains(nameof(PingMessage), entry.Message);
        }

        [Fact]
        public async Task WithStepNotCallingNext_RecordsNoResponseTelemetry()
        {
            var instrumentation = new CapturingHandlerInstrumentation();
            var (strategy, _) = CreateStrategy(new CapturingSender(), instrumentation: instrumentation, runner: CreateRunner(typeof(SkippingStep)));
            using var scope = CreateScope([], typeof(SkippingStep));

            await strategy.HandleMessage(CreateReceivedMessage(), scope.ServiceProvider, CancellationToken.None);

            Assert.Empty(instrumentation.ResponseStarted);
            Assert.Empty(instrumentation.ResponseSent);
            Assert.Empty(instrumentation.ResponseFailed);
        }

        [Fact]
        public async Task WithThrowingStep_PropagatesExceptionAndSendsNoReply()
        {
            var log = new List<string>();
            var sender = new CapturingSender();
            var (strategy, _) = CreateStrategy(sender, runner: CreateRunner(typeof(ThrowingStep)));
            using var scope = CreateScope(log, typeof(ThrowingStep));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => strategy.HandleMessage(CreateReceivedMessage(), scope.ServiceProvider, CancellationToken.None));

            Assert.Null(sender.SentMessage);
            Assert.Empty(log);
        }

        [Fact]
        public async Task WithThrowingHandler_PropagatesExceptionThroughStepAndSendsNoReply()
        {
            var log = new List<string>();
            var sender = new CapturingSender();
            var (strategy, _) = CreateStrategy(sender, runner: CreateRunner(typeof(LoggingStep)));
            using var scope = CreateScope(log, typeof(LoggingStep), throwingHandler: true);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => strategy.HandleMessage(CreateReceivedMessage(), scope.ServiceProvider, CancellationToken.None));

            Assert.Null(sender.SentMessage);
            Assert.Equal(["step:before"], log);
        }

        [Fact]
        public async Task WithStepForOtherMessageType_DoesNotRunStep()
        {
            var log = new List<string>();
            var (strategy, _) = CreateStrategy(new CapturingSender(), runner: CreateRunner(typeof(OtherMessageOnlyStep)));
            using var scope = CreateScope(log, typeof(OtherMessageOnlyStep));

            await strategy.HandleMessage(CreateReceivedMessage(), scope.ServiceProvider, CancellationToken.None);

            Assert.Equal(["handler"], log);
        }

        [Fact]
        public async Task WhenReplySendFails_StepsRunAgainOnRedelivery()
        {
            var log = new List<string>();
            var failingOnce = new FailingOnceSender();
            var (strategy, _) = CreateStrategy(new CapturingSender(), new FakeSenderFactory(null, failingOnce), runner: CreateRunner(typeof(LoggingStep)));
            using var scope = CreateScope(log, typeof(LoggingStep));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => strategy.HandleMessage(CreateReceivedMessage(), scope.ServiceProvider, CancellationToken.None));
            await strategy.HandleMessage(CreateReceivedMessage(), scope.ServiceProvider, CancellationToken.None);

            Assert.Equal(["step:before", "handler", "step:after", "step:before", "handler", "step:after"], log);
            Assert.NotNull(failingOnce.SentMessage);
        }
    }

    public class OnRetriesExhausted
    {
        [Fact]
        public async Task DeadLettersMessageWithExceptionMessage()
        {
            var (strategy, _) = CreateStrategy(new CapturingSender());
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("p"),
                new MessageProperties("m", "c", ReplyTo: "r"));
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            await strategy.OnRetriesExhausted(
                receivedMessage,
                new InvalidOperationException("handler blew up"),
                serviceProvider,
                CancellationToken.None);

            Assert.True(receivedMessage.WasDeadLettered);
            Assert.Equal("handler blew up", receivedMessage.DeadLetterReason);
        }
    }

    public class OnMessageHandlingError
    {
        [Fact]
        public async Task LogsExceptionAtErrorLevel()
        {
            var (strategy, _) = CreateStrategy(new CapturingSender());
            var logger = new FakeLogger();
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var exception = new InvalidOperationException("broker error");

            await strategy.OnMessageHandlingError(
                logger,
                serviceProvider,
                new ErrorDetails(exception, "endpoint"));

            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Error, logger.Messages[0].Level);
            Assert.Equal("Message processor error", logger.Messages[0].Message);
            Assert.Same(exception, logger.Messages[0].Exception);
        }
    }

    private static (CallMessageHandlingStrategy<PingMessage, PongMessage> Strategy, EchoCallHandler Handler) CreateStrategy(
        IMessageBusSender sender,
        FakeSenderFactory? senderFactory = null,
        IHandlerInstrumentation? instrumentation = null,
        IHandlerStepRunner? runner = null,
        ILogger<CallMessageHandlingStrategy<PingMessage, PongMessage>>? logger = null)
    {
        senderFactory ??= new FakeSenderFactory(null, sender);
        var fakeClient = new FakeMessageBusClient(senderFactory);
        var registry = new FakeClientRegistry(fakeClient);
        var handlerSettings = new HandlerSettings<PingMessage>("ping", "stub", "EchoCallHandler", false, 1, 0, 0, 1, TimeSpan.FromSeconds(30));
        var handler = new EchoCallHandler();
        var strategy = new CallMessageHandlingStrategy<PingMessage, PongMessage>(
            registry,
            handlerSettings,
            instrumentation ?? new NullHandlerInstrumentation(),
            runner ?? HandlerStepRunner.None,
            logger ?? new FakeTypedLogger<CallMessageHandlingStrategy<PingMessage, PongMessage>>());
        return (strategy, handler);
    }

    private static FakeCallReceivedMessage CreateReceivedMessage(string payload = "payload")
        => new(new PingMessage(payload), new MessageProperties("msg-1", "corr-1", ReplyTo: "reply.sender"));

    private static ServiceProvider CreateServiceProvider(List<string> log, Type[] stepTypes, bool throwingHandler)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<ScopedState>();

        if (throwingHandler)
        {
            services.AddScoped<CallHandler<PingMessage, PongMessage>, ThrowingCallHandler>();
        }
        else
        {
            services.AddScoped<CallHandler<PingMessage, PongMessage>, LoggingEchoCallHandler>();
        }

        foreach (var stepType in stepTypes)
        {
            services.AddScoped(stepType);
        }

        return services.BuildServiceProvider();
    }

    private static IServiceScope CreateScope(List<string> log, Type stepType, bool throwingHandler = false)
        => CreateServiceProvider(log, [stepType], throwingHandler).CreateScope();

    private static HandlerStepRunner CreateRunner(params Type[] stepTypes)
        => new(stepTypes.Select(stepType => new HandlerStepRegistration(stepType, HandlerStepRegistration.ResolveFilterType(stepType))));

    private sealed class ScopedState
    {
        public string? Value { get; set; }
    }

    private sealed class LoggingEchoCallHandler(List<string> log, ScopedState scopedState) : CallHandler<PingMessage, PongMessage>
    {
        protected override Task<PongMessage> Handle(IMessageContext<PingMessage> messageContext)
        {
            log.Add(scopedState.Value is null ? "handler" : $"handler:state={scopedState.Value}");
            return Task.FromResult(new PongMessage($"echo: {messageContext.Message.Payload}"));
        }
    }

    private sealed class ThrowingCallHandler : CallHandler<PingMessage, PongMessage>
    {
        protected override Task<PongMessage> Handle(IMessageContext<PingMessage> messageContext)
            => throw new InvalidOperationException("handler failed");
    }

    private sealed class LoggingStep(List<string> log) : HandlerStep<PingMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<PingMessage> context)
        {
            log.Add("step:before");
            return Task.CompletedTask;
        }

        protected override Task OnAfterHandlerExecution(HandlerStepContext<PingMessage> context)
        {
            log.Add("step:after");
            return Task.CompletedTask;
        }
    }

    private sealed class SkippingStep(List<string> log) : HandlerStep<PingMessage>
    {
        public override Task Handle(HandlerStepContext<PingMessage> context, HandlerStepDelegate next)
        {
            log.Add("step:skipped");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStep : HandlerStep<PingMessage>
    {
        public override Task Handle(HandlerStepContext<PingMessage> context, HandlerStepDelegate next)
            => throw new InvalidOperationException("step failed");
    }

    private sealed class OtherMessageOnlyStep(List<string> log) : HandlerStep<OtherMessage>
    {
        public override Task Handle(HandlerStepContext<OtherMessage> context, HandlerStepDelegate next)
        {
            log.Add("other-step");
            return next();
        }
    }

    private sealed class ScopedStateStep(ScopedState scopedState) : HandlerStep<PingMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<PingMessage> context)
        {
            scopedState.Value = "from-step";
            return Task.CompletedTask;
        }
    }

    private sealed class ContextLoggingStep(List<string> log) : HandlerStep<PingMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<PingMessage> context)
        {
            log.Add($"step:{context.HandlerType.Name}:{context.Message.Payload}:{context.Properties.MessageId}");
            return Task.CompletedTask;
        }
    }

    private record OtherMessage(string Value);

    private sealed class LoggingSender(List<string> log) : IMessageBusSender
    {
        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            log.Add("reply");
            return Task.CompletedTask;
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FailingOnceSender : IMessageBusSender
    {
        private bool _hasFailed;

        public object? SentMessage { get; private set; }

        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            if (!_hasFailed)
            {
                _hasFailed = true;
                throw new InvalidOperationException("transport error");
            }

            SentMessage = message;
            return Task.CompletedTask;
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTypedLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Messages { get; } = [];
        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add((logLevel, formatter(state, exception), exception));
    }

    private record PingMessage(string Payload) : IReturns<PongMessage>;

    private record PongMessage(string Echo) : IOpenTelemetryTagsProvider
    {
        public IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags() =>
            [new("echo.value", Echo)];
    }

    private sealed class EchoCallHandler : CallHandler<PingMessage, PongMessage>
    {
        protected override Task<PongMessage> Handle(IMessageContext<PingMessage> messageContext)
            => Task.FromResult(new PongMessage($"echo: {messageContext.Message.Payload}"));
    }

    private sealed class CapturingSender : IMessageBusSender
    {
        public object? SentMessage { get; private set; }
        public MessageProperties? SentProperties { get; private set; }

        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            SentMessage = message;
            SentProperties = messageProperties;
            return Task.CompletedTask;
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSenderFactory(string? _, IMessageBusSender sender)
    {
        public string? RequestedQueueName { get; private set; }

        public Task<IMessageBusSender> CreateSender(string queueName)
        {
            RequestedQueueName = queueName;
            return Task.FromResult(sender);
        }
    }

    private sealed class FakeMessageBusClient(FakeSenderFactory senderFactory) : IMessageBusClient
    {
        public string MessagingSystem => "stub";
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => senderFactory.CreateSender(queueName);
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

    private sealed class FakeCallReceivedMessage(PingMessage body, MessageProperties properties) : IReceivedMessage<PingMessage>
    {
        public bool WasDeadLettered { get; private set; }
        public string? DeadLetterReason { get; private set; }
        public PingMessage Body => body;
        public MessageProperties Properties => properties;
        public Task Complete(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
        {
            WasDeadLettered = true;
            DeadLetterReason = reason;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Messages { get; } = [];
        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class ThrowingSender(Exception exception) : IMessageBusSender
    {
        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default) =>
            Task.FromException(exception);
        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingHandlerInstrumentation : IHandlerInstrumentation
    {
        public List<(string CallQueueName, string ReplyQueueName, string MessagingSystem, string MessageId, string CorrelationId)> ResponseStarted { get; } = [];
        public List<(string CallQueueName, double DurationMs)> ResponseSent { get; } = [];
        public List<string> ResponseFailed { get; } = [];

        public bool IsEnabled => true;

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default) => null;

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<ActivityLink> links) => null;

        public void RecordReceived(string handlerName, string queueName, long count = 1) { }

        public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) { }

        public void RecordFailed(string handlerName, string queueName, long count = 1) { }

        public void RecordRetried(string handlerName, string queueName, long count = 1) { }

        public void RecordHalted(string handlerName, string queueName, string reason) { }

        public void RecordStreamPartitionTransition(string handlerName, string queueName, string consumerName, int? partition, StreamPartitionState state) { }

        public void RecordCheckpointSaved(string handlerName, string queueName, double durationMs) { }

        public void RecordCheckpointSaveFailed(string handlerName, string queueName) { }

        public void RecordCheckpointResolved(string handlerName, string queueName, CheckpointResolution resolution) { }

        public Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId)
        {
            ResponseStarted.Add((callQueueName, replyQueueName, messagingSystem, messageId, correlationId));
            return null;
        }

        public void RecordResponseSent(string callQueueName, double durationMs) => ResponseSent.Add((callQueueName, durationMs));

        public void RecordResponseFailed(string callQueueName) => ResponseFailed.Add(callQueueName);
    }

    private sealed class ActivityCapturingHandlerInstrumentation : IHandlerInstrumentation
    {
        public Activity? CapturedActivity { get; private set; }

        public bool IsEnabled => true;

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default) => null;

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<ActivityLink> links) => null;

        public void RecordReceived(string handlerName, string queueName, long count = 1) { }

        public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) { }

        public void RecordFailed(string handlerName, string queueName, long count = 1) { }

        public void RecordRetried(string handlerName, string queueName, long count = 1) { }

        public void RecordHalted(string handlerName, string queueName, string reason) { }

        public void RecordStreamPartitionTransition(string handlerName, string queueName, string consumerName, int? partition, StreamPartitionState state) { }

        public void RecordCheckpointSaved(string handlerName, string queueName, double durationMs) { }

        public void RecordCheckpointSaveFailed(string handlerName, string queueName) { }

        public void RecordCheckpointResolved(string handlerName, string queueName, CheckpointResolution resolution) { }

        public Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId)
        {
            CapturedActivity = new Activity("flowly.call.reply test").Start();
            return CapturedActivity;
        }

        public void RecordResponseSent(string callQueueName, double durationMs) { }

        public void RecordResponseFailed(string callQueueName) { }
    }

    public class ResponseSendInstrumentation
    {
        [Fact]
        public async Task StartsResponseSendingWithCorrectArgs()
        {
            var instrumentation = new CapturingHandlerInstrumentation();
            var sender = new CapturingSender();
            var (strategy, handler) = CreateStrategy(sender, instrumentation: instrumentation);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("payload"),
                new MessageProperties("msg-1", "corr-1", ReplyTo: "ping-reply-sender"));

            var services = new ServiceCollection();
            services.AddScoped<CallHandler<PingMessage, PongMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            await strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.Single(instrumentation.ResponseStarted);
            Assert.Equal("ping", instrumentation.ResponseStarted[0].CallQueueName);
            Assert.Equal("ping-reply-sender", instrumentation.ResponseStarted[0].ReplyQueueName);
            Assert.Equal("stub", instrumentation.ResponseStarted[0].MessagingSystem);
            Assert.True(Guid.TryParse(instrumentation.ResponseStarted[0].MessageId, out _));
            Assert.Equal("corr-1", instrumentation.ResponseStarted[0].CorrelationId);
        }

        [Fact]
        public async Task RecordsResponseSentOnSuccess()
        {
            var instrumentation = new CapturingHandlerInstrumentation();
            var (strategy, handler) = CreateStrategy(new CapturingSender(), instrumentation: instrumentation);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("payload"),
                new MessageProperties("msg-1", "corr-1", ReplyTo: "ping-reply-sender"));

            var services = new ServiceCollection();
            services.AddScoped<CallHandler<PingMessage, PongMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            await strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.Single(instrumentation.ResponseSent);
            Assert.Equal("ping", instrumentation.ResponseSent[0].CallQueueName);
            Assert.True(instrumentation.ResponseSent[0].DurationMs >= 0);
            Assert.Empty(instrumentation.ResponseFailed);
        }

        [Fact]
        public async Task WhenResponseSendFails_RecordsResponseFailedAndRethrows()
        {
            var instrumentation = new CapturingHandlerInstrumentation();
            var throwingSender = new ThrowingSender(new InvalidOperationException("transport error"));
            var (strategy, handler) = CreateStrategy(new CapturingSender(), new FakeSenderFactory(null, throwingSender), instrumentation);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("payload"),
                new MessageProperties("msg-1", "corr-1", ReplyTo: "ping-reply-sender"));

            var services = new ServiceCollection();
            services.AddScoped<CallHandler<PingMessage, PongMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None));

            Assert.Single(instrumentation.ResponseFailed);
            Assert.Equal("ping", instrumentation.ResponseFailed[0]);
            Assert.Empty(instrumentation.ResponseSent);
        }

        [Fact]
        public async Task WhenReturnMessageImplementsIOpenTelemetryTagsProvider_SetsTagsOnResponseSpan()
        {
            var instrumentation = new ActivityCapturingHandlerInstrumentation();
            var (strategy, handler) = CreateStrategy(new CapturingSender(), instrumentation: instrumentation);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("payload"),
                new MessageProperties("msg-1", "corr-1", ReplyTo: "ping-reply-sender"));

            var services = new ServiceCollection();
            services.AddScoped<CallHandler<PingMessage, PongMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            await strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.Equal("echo: payload", instrumentation.CapturedActivity?.GetTagItem("echo.value"));
        }
    }
}
