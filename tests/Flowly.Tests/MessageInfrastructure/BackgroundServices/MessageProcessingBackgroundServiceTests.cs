using Flowly.MessageInfrastructure.HandlerSteps;
using Flowly.MessageInfrastructure.StreamPartitions;
using System.Diagnostics;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.MessageHandlingStrategies;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class MessageProcessingBackgroundServiceTests
{
    private static (MessageProcessingBackgroundService<TestMessage> messageProcessingBackgroundService, FakeMessageBusClient<TestMessage> client) Build(
        string queueName = "test-queue",
        bool readAndDelete = false,
        int maxConcurrentCalls = 1,
        RecordingMessageHandler? handler = null,
        FakeServiceScopeFactory<MessageHandler<TestMessage>>? scopeFactory = null,
        IHandlerInstrumentation? instrumentation = null)
    {
        var client = new FakeMessageBusClient<TestMessage>();
        var clientRegistry = new FakeMessageBusClientRegistry(client);
        var settings = new HandlerSettings<TestMessage>(queueName, "azure-service-bus", "TestHandler", readAndDelete, maxConcurrentCalls, 0, 0, 0, TimeSpan.Zero);
        var factory = scopeFactory ?? new FakeServiceScopeFactory<MessageHandler<TestMessage>>(handler ?? new RecordingMessageHandler());
        var messageProcessingBackgroundService = new MessageProcessingBackgroundService<TestMessage>(
            clientRegistry,
            factory,
            settings,
            NullLogger<MessageProcessingBackgroundService<TestMessage>>.Instance,
            instrumentation ?? new NullHandlerInstrumentation(),
            new StandardMessageHandlingStrategy<TestMessage>(HandlerStepRunner.None));
        return (messageProcessingBackgroundService, client);
    }

    private static (MessageProcessingBackgroundService<TestMessage> messageProcessingBackgroundService, FakeMessageBusClient<TestMessage> client) BuildWithSteps(
        List<string> log,
        Type[] stepTypes,
        int maxRetries = 0,
        IHandlerInstrumentation? instrumentation = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<ScopedState>();
        services.AddScoped<MessageHandler<TestMessage>, StepAwareMessageHandler>();

        foreach (var stepType in stepTypes)
        {
            services.AddScoped(stepType);
        }

        var serviceProvider = services.BuildServiceProvider();
        var handlerStepRunner = new HandlerStepRunner(stepTypes.Select(stepType => new HandlerStepRegistration(stepType, HandlerStepRegistration.ResolveFilterType(stepType))));
        var client = new FakeMessageBusClient<TestMessage>();
        var settings = new HandlerSettings<TestMessage>("test-queue", "azure-service-bus", "TestHandler", false, 1, maxRetries, 0, 0, TimeSpan.Zero);
        var messageProcessingBackgroundService = new MessageProcessingBackgroundService<TestMessage>(
            new FakeMessageBusClientRegistry(client),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            settings,
            NullLogger<MessageProcessingBackgroundService<TestMessage>>.Instance,
            instrumentation ?? new NullHandlerInstrumentation(),
            new StandardMessageHandlingStrategy<TestMessage>(handlerStepRunner));
        return (messageProcessingBackgroundService, client);
    }

    private static async Task<FakeMessageBusProcessor<TestMessage>> StartProcessing(MessageProcessingBackgroundService<TestMessage> messageProcessingBackgroundService, FakeMessageBusClient<TestMessage> client)
    {
        await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
        var processor = await client.ProcessorCreated;
        await processor.Started;
        return processor;
    }

    private static (MessageProcessingBackgroundService<TaggedTestMessage> service, FakeMessageBusClient<TaggedTestMessage> client) BuildTagged(
        IHandlerInstrumentation? instrumentation = null)
    {
        var client = new FakeMessageBusClient<TaggedTestMessage>();
        var clientRegistry = new FakeMessageBusClientRegistry(client);
        var settings = new HandlerSettings<TaggedTestMessage>("tagged-queue", "azure-service-bus", "TaggedTestHandler", false, 1, 0, 0, 0, TimeSpan.Zero);
        var factory = new FakeServiceScopeFactory<MessageHandler<TaggedTestMessage>>(new NoOpTaggedHandler());
        var service = new MessageProcessingBackgroundService<TaggedTestMessage>(
            clientRegistry,
            factory,
            settings,
            NullLogger<MessageProcessingBackgroundService<TaggedTestMessage>>.Instance,
            instrumentation ?? new NullHandlerInstrumentation(),
            new StandardMessageHandlingStrategy<TaggedTestMessage>(HandlerStepRunner.None));
        return (service, client);
    }

    public class Execute
    {
        [Fact]
        public async Task CreatesProcessorWithQueueNameFromSettings()
        {
            var (messageProcessingBackgroundService, client) = Build();

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal("test-queue", processor.QueueName);
        }

        [Fact]
        public async Task CreatesProcessorWithMaxConcurrentCallsFromSettings()
        {
            var (messageProcessingBackgroundService, client) = Build(maxConcurrentCalls: 3);

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(3, processor.Options.MaxConcurrentCalls);
        }

        [Fact]
        public async Task CreatesProcessorWithPeekLockModeWhenReadAndDeleteIsFalse()
        {
            var (messageProcessingBackgroundService, client) = Build();

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(MessageBusReceiveMode.PeekLock, processor.Options.ReceiveMode);
        }

        [Fact]
        public async Task CreatesProcessorWithReceiveAndDeleteModeWhenReadAndDeleteIsTrue()
        {
            var (messageProcessingBackgroundService, client) = Build("test-queue", true);

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(MessageBusReceiveMode.ReceiveAndDelete, processor.Options.ReceiveMode);
        }

        [Fact]
        public async Task StartsProcessingMessages()
        {
            var (messageProcessingBackgroundService, client) = Build();

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(processor.StartProcessingWasCalled);
        }
    }

    public class OnProcessMessage
    {
        [Fact]
        public async Task InvokesHandlerWithMessageBody()
        {
            var handler = new RecordingMessageHandler();
            var (messageProcessingBackgroundService, client) = Build(handler: handler);

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var message = new TestMessage("hello");
            await processor.FireProcessMessage(new FakeReceivedMessage<TestMessage>(message));

            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(message, handler.ReceivedMessage);
        }

        [Fact]
        public async Task CreatesNewScopePerMessage()
        {
            var scopeFactory = new FakeServiceScopeFactory<MessageHandler<TestMessage>>(new RecordingMessageHandler());
            var (messageProcessingBackgroundService, client) = Build(scopeFactory: scopeFactory);

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            await processor.FireProcessMessage(new FakeReceivedMessage<TestMessage>(new TestMessage("a")));
            await processor.FireProcessMessage(new FakeReceivedMessage<TestMessage>(new TestMessage("b")));

            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(2, scopeFactory.ScopesCreated);
        }
    }

    public class OnProcessMessageWithCorruptBody
    {
        [Fact]
        public async Task DeadLettersMessage()
        {
            var (messageProcessingBackgroundService, client) = Build();

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var poisonMessage = new ThrowingReceivedMessage<TestMessage>();
            await processor.FireProcessMessage(poisonMessage);

            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(poisonMessage.DeadLettered);
        }

        [Fact]
        public async Task DoesNotInvokeHandler()
        {
            var handler = new RecordingMessageHandler();
            var (messageProcessingBackgroundService, client) = Build(handler: handler);

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            await processor.FireProcessMessage(new ThrowingReceivedMessage<TestMessage>());

            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Null(handler.ReceivedMessage);
        }

        [Fact]
        public async Task DoesNotThrow()
        {
            var (messageProcessingBackgroundService, client) = Build();

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var exception = await Record.ExceptionAsync(() =>
                processor.FireProcessMessage(new ThrowingReceivedMessage<TestMessage>()));

            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Null(exception);
        }
    }

    public class OnProcessMessageWithHandlerSteps
    {
        [Fact]
        public async Task RunsStepAroundHandlerAndCompletesMessage()
        {
            var log = new List<string>();
            var (messageProcessingBackgroundService, client) = BuildWithSteps(log, [typeof(LoggingStep)]);
            var processor = await StartProcessing(messageProcessingBackgroundService, client);
            var receivedMessage = new FakeReceivedMessage<TestMessage>(new TestMessage("hello"));

            await processor.FireProcessMessage(receivedMessage);
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(["step:before", "handler", "step:after"], log);
            Assert.True(receivedMessage.Completed);
        }

        [Fact]
        public async Task StepAndHandlerShareScopedService()
        {
            var log = new List<string>();
            var (messageProcessingBackgroundService, client) = BuildWithSteps(log, [typeof(ScopedStateStep)]);
            var processor = await StartProcessing(messageProcessingBackgroundService, client);

            await processor.FireProcessMessage(new FakeReceivedMessage<TestMessage>(new TestMessage("hello")));
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(["handler:state=from-step"], log);
        }

        [Fact]
        public async Task StepRunsInsideHandlingActivity()
        {
            var log = new List<string>();
            var instrumentation = new ActivityStartingHandlerInstrumentation();
            var (messageProcessingBackgroundService, client) = BuildWithSteps(log, [typeof(ActivityRecordingStep)], instrumentation: instrumentation);
            var processor = await StartProcessing(messageProcessingBackgroundService, client);

            await processor.FireProcessMessage(new FakeReceivedMessage<TestMessage>(new TestMessage("hello")));
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal([$"step:{instrumentation.Activity?.Id}", "handler"], log);
        }

        [Fact]
        public async Task WithStepNotCallingNext_CompletesMessageWithoutInvokingHandler()
        {
            var log = new List<string>();
            var (messageProcessingBackgroundService, client) = BuildWithSteps(log, [typeof(SkippingStep)]);
            var processor = await StartProcessing(messageProcessingBackgroundService, client);
            var receivedMessage = new FakeReceivedMessage<TestMessage>(new TestMessage("hello"));

            await processor.FireProcessMessage(receivedMessage);
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(["step:skipped"], log);
            Assert.True(receivedMessage.Completed);
        }

        [Fact]
        public async Task WithStepSwallowingHandlerException_CompletesMessage()
        {
            var log = new List<string>();
            var (messageProcessingBackgroundService, client) = BuildWithSteps(log, [typeof(SwallowingStep), typeof(ThrowingHandlerTriggerStep)]);
            var processor = await StartProcessing(messageProcessingBackgroundService, client);
            var receivedMessage = new FakeReceivedMessage<TestMessage>(new TestMessage("hello"));

            await processor.FireProcessMessage(receivedMessage);
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(receivedMessage.Completed);
            Assert.False(receivedMessage.DeadLettered);
        }

        [Fact]
        public async Task WithThrowingStepAndNoRetriesLeft_DeadLettersMessage()
        {
            var log = new List<string>();
            var (messageProcessingBackgroundService, client) = BuildWithSteps(log, [typeof(ThrowingStep)]);
            var processor = await StartProcessing(messageProcessingBackgroundService, client);
            var receivedMessage = new FakeReceivedMessage<TestMessage>(new TestMessage("hello"));

            await processor.FireProcessMessage(receivedMessage);
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(receivedMessage.DeadLettered);
            Assert.Equal("step failed", receivedMessage.DeadLetterReason);
            Assert.Empty(log);
        }

        [Fact]
        public async Task WithThrowingStepAndRetriesLeft_RepublishesMessageWithIncrementedRetryCount()
        {
            var log = new List<string>();
            var (messageProcessingBackgroundService, client) = BuildWithSteps(log, [typeof(ThrowingStep)], maxRetries: 2);
            var processor = await StartProcessing(messageProcessingBackgroundService, client);
            var receivedMessage = new FakeReceivedMessage<TestMessage>(new TestMessage("hello"));

            await processor.FireProcessMessage(receivedMessage);
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal([1], client.RepublishedRetryCounts);
            Assert.True(receivedMessage.Completed);
            Assert.False(receivedMessage.DeadLettered);
        }

        [Fact]
        public async Task WithThrowingHandlerAndRetriesLeft_RepublishesMessageAfterStepsRan()
        {
            var log = new List<string>();
            var (messageProcessingBackgroundService, client) = BuildWithSteps(log, [typeof(LoggingStep), typeof(ThrowingHandlerTriggerStep)], maxRetries: 2);
            var processor = await StartProcessing(messageProcessingBackgroundService, client);

            await processor.FireProcessMessage(new FakeReceivedMessage<TestMessage>(new TestMessage("hello")));
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(["step:before", "handler:throwing"], log);
            Assert.Equal([1], client.RepublishedRetryCounts);
        }

        [Fact]
        public async Task WithCorruptBody_DeadLettersBeforeAnyStepRuns()
        {
            var log = new List<string>();
            var (messageProcessingBackgroundService, client) = BuildWithSteps(log, [typeof(LoggingStep)]);
            var processor = await StartProcessing(messageProcessingBackgroundService, client);
            var poisonMessage = new ThrowingReceivedMessage<TestMessage>();

            await processor.FireProcessMessage(poisonMessage);
            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(poisonMessage.DeadLettered);
            Assert.Empty(log);
        }
    }

    public class OnProcessError
    {
        [Fact]
        public async Task DoesNotThrow()
        {
            var (messageProcessingBackgroundService, client) = Build();

            await messageProcessingBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var exception = await Record.ExceptionAsync(() =>
                processor.FireProcessError(new ErrorDetails(new Exception("boom"), "endpoint")));

            await messageProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Null(exception);
        }
    }

    public class WhenMessageImplementsIOpenTelemetryTagsProvider
    {
        [Fact]
        public async Task SetsTagsOnHandlerSpan()
        {
            var instrumentation = new ActivityReturningHandlerInstrumentation();
            var (service, client) = BuildTagged(instrumentation);

            await service.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            await processor.FireProcessMessage(new FakeReceivedMessage<TaggedTestMessage>(new TaggedTestMessage("order-123")));

            await service.StopAsync(CancellationToken.None);

            Assert.Equal("order-123", instrumentation.Activity.GetTagItem("order.id"));
        }

        [Fact]
        public async Task WhenMessageDoesNotImplementInterface_SetsNoCustomTags()
        {
            var instrumentation = new ActivityReturningHandlerInstrumentation();
            var (service, client) = Build(instrumentation: instrumentation);

            await service.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            await processor.FireProcessMessage(new FakeReceivedMessage<TestMessage>(new TestMessage("hello")));

            await service.StopAsync(CancellationToken.None);

            Assert.Null(instrumentation.Activity.GetTagItem("order.id"));
        }
    }

    private record TestMessage(string Value);

    private record TaggedTestMessage(string OrderId) : IOpenTelemetryTagsProvider
    {
        public IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags() =>
            [new("order.id", OrderId)];
    }

    private class NoOpTaggedHandler : MessageHandler<TaggedTestMessage>
    {
        public override Task Handle(IMessageContext<TaggedTestMessage> ctx) => Task.CompletedTask;
    }

    private class ActivityReturningHandlerInstrumentation : IHandlerInstrumentation
    {
        public Activity Activity { get; } = new Activity("flowly.handle test").Start();

        public bool IsEnabled => true;

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default) => Activity;

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<ActivityLink> links) => Activity;

        public void RecordReceived(string handlerName, string queueName, long count = 1) { }

        public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) { }

        public void RecordFailed(string handlerName, string queueName, long count = 1) { }

        public void RecordRetried(string handlerName, string queueName, long count = 1) { }

        public void RecordHalted(string handlerName, string queueName, string reason) { }

        public void RecordStreamPartitionTransition(string handlerName, string queueName, string consumerName, int? partition, StreamPartitionState state) { }

        public void RecordCheckpointSaved(string handlerName, string queueName, double durationMs) { }

        public void RecordCheckpointSaveFailed(string handlerName, string queueName) { }

        public void RecordCheckpointResolved(string handlerName, string queueName, CheckpointResolution resolution) { }

        public Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId) => null;

        public void RecordResponseSent(string callQueueName, double durationMs) { }

        public void RecordResponseFailed(string callQueueName) { }
    }

    private class RecordingMessageHandler : MessageHandler<TestMessage>
    {
        public TestMessage? ReceivedMessage { get; private set; }

        public override Task Handle(IMessageContext<TestMessage> ctx)
        {
            ReceivedMessage = ctx.Message;
            return Task.CompletedTask;
        }
    }

    private sealed class ScopedState
    {
        public string? Value { get; set; }
        public bool ThrowInHandler { get; set; }
    }

    private sealed class StepAwareMessageHandler(List<string> log, ScopedState scopedState) : MessageHandler<TestMessage>
    {
        public override Task Handle(IMessageContext<TestMessage> messageContext)
        {
            if (scopedState.ThrowInHandler)
            {
                log.Add("handler:throwing");
                throw new InvalidOperationException("handler failed");
            }

            log.Add(scopedState.Value is null ? "handler" : $"handler:state={scopedState.Value}");
            return Task.CompletedTask;
        }
    }

    private sealed class LoggingStep(List<string> log) : HandlerStep<TestMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<TestMessage> context)
        {
            log.Add("step:before");
            return Task.CompletedTask;
        }

        protected override Task OnAfterHandlerExecution(HandlerStepContext<TestMessage> context)
        {
            log.Add("step:after");
            return Task.CompletedTask;
        }
    }

    private sealed class ScopedStateStep(ScopedState scopedState) : HandlerStep<TestMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<TestMessage> context)
        {
            scopedState.Value = "from-step";
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandlerTriggerStep(ScopedState scopedState) : HandlerStep<TestMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<TestMessage> context)
        {
            scopedState.ThrowInHandler = true;
            return Task.CompletedTask;
        }
    }

    private sealed class SkippingStep(List<string> log) : HandlerStep<TestMessage>
    {
        public override Task Handle(HandlerStepContext<TestMessage> context, HandlerStepDelegate next)
        {
            log.Add("step:skipped");
            return Task.CompletedTask;
        }
    }

    private sealed class SwallowingStep : HandlerStep<TestMessage>
    {
        public override async Task Handle(HandlerStepContext<TestMessage> context, HandlerStepDelegate next)
        {
            try
            {
                await next();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private sealed class ThrowingStep : HandlerStep<TestMessage>
    {
        public override Task Handle(HandlerStepContext<TestMessage> context, HandlerStepDelegate next)
            => throw new InvalidOperationException("step failed");
    }

    private sealed class ActivityRecordingStep(List<string> log) : HandlerStep<TestMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<TestMessage> context)
        {
            log.Add($"step:{Activity.Current?.Id}");
            return Task.CompletedTask;
        }
    }

    private sealed class ActivityStartingHandlerInstrumentation : IHandlerInstrumentation
    {
        public Activity? Activity { get; private set; }

        public bool IsEnabled => true;

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default)
        {
            Activity = new Activity("flowly.handle test").Start();
            return Activity;
        }

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<ActivityLink> links)
            => StartHandling(handlerName, queueName, messagingSystem, messageProperties);

        public void RecordReceived(string handlerName, string queueName, long count = 1) { }

        public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) { }

        public void RecordFailed(string handlerName, string queueName, long count = 1) { }

        public void RecordRetried(string handlerName, string queueName, long count = 1) { }

        public void RecordHalted(string handlerName, string queueName, string reason) { }

        public void RecordStreamPartitionTransition(string handlerName, string queueName, string consumerName, int? partition, StreamPartitionState state) { }

        public void RecordCheckpointSaved(string handlerName, string queueName, double durationMs) { }

        public void RecordCheckpointSaveFailed(string handlerName, string queueName) { }

        public void RecordCheckpointResolved(string handlerName, string queueName, CheckpointResolution resolution) { }

        public Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId) => null;

        public void RecordResponseSent(string callQueueName, double durationMs) { }

        public void RecordResponseFailed(string callQueueName) { }
    }

    private sealed class RecordingMessageBusSender(List<int> republishedRetryCounts) : IMessageBusSender
    {
        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            republishedRetryCounts.Add(messageProperties.RetryCount);
            return Task.CompletedTask;
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class FakeMessageBusClient<TMsg> : IMessageBusClient where TMsg : class
    {
        private readonly TaskCompletionSource<FakeMessageBusProcessor<TMsg>> _processorCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<FakeMessageBusProcessor<TMsg>> ProcessorCreated => _processorCreated.Task;

        public string MessagingSystem => "fake";

        public List<int> RepublishedRetryCounts { get; } = [];

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            var processor = new FakeMessageBusProcessor<TMessage>(queueName, options);
            _processorCreated.SetResult((FakeMessageBusProcessor<TMsg>)(object)processor);
            return Task.FromResult<IMessageBusProcessor<TMessage>>(processor);
        }

        public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        {
            throw new NotImplementedException();
        }

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        {
            throw new NotImplementedException();
        }

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
            => Task.FromResult<IMessageBusSender>(new RecordingMessageBusSender(RepublishedRetryCounts));

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        {
            throw new NotImplementedException();
        }

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private class FakeMessageBusProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) : IMessageBusProcessor<TMessage>
    {
        private readonly TaskCompletionSource _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string QueueName { get; } = queueName;
        public MessageBusProcessorOptions Options { get; } = options;
        public bool StartProcessingWasCalled { get; private set; }
        public Task Started => _startedTcs.Task;

        public event Func<IReceivedMessage<TMessage>, CancellationToken, Task>? ProcessMessage;
        public event Func<ErrorDetails, Task>? ProcessError;

        public Task StartProcessingMessages(CancellationToken cancellationToken = default)
        {
            StartProcessingWasCalled = true;
            _startedTcs.SetResult();
            return Task.CompletedTask;
        }

        public Task StopProcessing(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task FireProcessMessage(IReceivedMessage<TMessage> message)
        {
            return ProcessMessage?.Invoke(message, CancellationToken.None) ?? Task.CompletedTask;
        }

        public Task FireProcessError(ErrorDetails error)
        {
            return ProcessError?.Invoke(error) ?? Task.CompletedTask;
        }
    }
}