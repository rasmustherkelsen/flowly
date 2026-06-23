using System.Diagnostics;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.MessageHandlingStrategies;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
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
            new StandardMessageHandlingStrategy<TestMessage>());
        return (messageProcessingBackgroundService, client);
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
            new StandardMessageHandlingStrategy<TaggedTestMessage>());
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

    private class FakeMessageBusClient<TMsg> : IMessageBusClient where TMsg : class
    {
        private readonly TaskCompletionSource<FakeMessageBusProcessor<TMsg>> _processorCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<FakeMessageBusProcessor<TMsg>> ProcessorCreated => _processorCreated.Task;

        public string MessagingSystem => "fake";

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
        {
            throw new NotImplementedException();
        }

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