using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class MessageProcessingBackgroundServiceTests
{
    private static (MessageProcessingBackgroundService<TestMessage> messageProcessingBackgroundService, FakeMessageBusClient client) Build(
        string queueName = "test-queue",
        bool readAndDelete = false,
        int maxConcurrentCalls = 1,
        RecordingMessageHandler? handler = null,
        FakeServiceScopeFactory<MessageHandler<TestMessage>>? scopeFactory = null)
    {
        var client = new FakeMessageBusClient();
        var clientRegistry = new FakeMessageBusClientRegistry(client);
        var settings = new HandlerSettings<TestMessage>(queueName, "azure-service-bus", "TestHandler", readAndDelete, maxConcurrentCalls);
        var factory = scopeFactory ?? new FakeServiceScopeFactory<MessageHandler<TestMessage>>(handler ?? new RecordingMessageHandler());
        var messageProcessingBackgroundService = new MessageProcessingBackgroundService<TestMessage>(
            clientRegistry,
            factory,
            settings,
            NullLogger<MessageProcessingBackgroundService<TestMessage>>.Instance,
            new NullHandlerInstrumentation(),
            new StandardMessageHandlingStrategy<TestMessage>());
        return (messageProcessingBackgroundService, client);
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

    private record TestMessage(string Value);

    private class RecordingMessageHandler : MessageHandler<TestMessage>
    {
        public TestMessage? ReceivedMessage { get; private set; }

        public override Task Handle(IMessageContext<TestMessage> ctx)
        {
            ReceivedMessage = ctx.Message;
            return Task.CompletedTask;
        }
    }

    private class FakeMessageBusClient : IMessageBusClient
    {
        private readonly TaskCompletionSource<FakeMessageBusProcessor<TestMessage>> _processorCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<FakeMessageBusProcessor<TestMessage>> ProcessorCreated => _processorCreated.Task;

        public string MessagingSystem => "fake";

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            var processor = new FakeMessageBusProcessor<TMessage>(queueName, options);
            _processorCreated.SetResult((FakeMessageBusProcessor<TestMessage>)(object)processor);
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