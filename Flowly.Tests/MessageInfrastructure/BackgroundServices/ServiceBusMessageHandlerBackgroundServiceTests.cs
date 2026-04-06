using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class ServiceBusMessageHandlerBackgroundServiceTests
{
    public class Execute
    {
        [Fact]
        public async Task CreatesProcessorWithQueueNameFromSettings()
        {
            var (serviceBusMessageHandlerBackgroundService, client) = Build("test-queue");

            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await serviceBusMessageHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal("test-queue", processor.QueueName);
        }

        [Fact]
        public async Task CreatesProcessorWithMaxConcurrentCallsFromSettings()
        {
            var (serviceBusMessageHandlerBackgroundService, client) = Build("test-queue", maxConcurrentCalls: 3);

            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await serviceBusMessageHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(3, processor.Options.MaxConcurrentCalls);
        }

        [Fact]
        public async Task CreatesProcessorWithPeekLockModeWhenReadAndDeleteIsFalse()
        {
            var (serviceBusMessageHandlerBackgroundService, client) = Build("test-queue", readAndDelete: false);

            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await serviceBusMessageHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(MessageBusReceiveMode.PeekLock, processor.Options.ReceiveMode);
        }

        [Fact]
        public async Task CreatesProcessorWithReceiveAndDeleteModeWhenReadAndDeleteIsTrue()
        {
            var (serviceBusMessageHandlerBackgroundService, client) = Build("test-queue", readAndDelete: true);

            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await serviceBusMessageHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(MessageBusReceiveMode.ReceiveAndDelete, processor.Options.ReceiveMode);
        }

        [Fact]
        public async Task StartsProcessingMessages()
        {
            var (serviceBusMessageHandlerBackgroundService, client) = Build("test-queue");

            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await serviceBusMessageHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(processor.StartProcessingWasCalled);
        }
    }

    public class OnProcessMessage
    {
        [Fact]
        public async Task InvokesHandlerWithMessageBody()
        {
            var handler = new RecordingMessageHandler();
            var (serviceBusMessageHandlerBackgroundService, client) = Build(handler: handler);

            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var message = new TestMessage("hello");
            await processor.FireProcessMessage(new FakeReceivedMessage<TestMessage>(message));

            await serviceBusMessageHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(message, handler.ReceivedMessage);
        }

        [Fact]
        public async Task CreatesNewScopePerMessage()
        {
            var scopeFactory = new FakeServiceScopeFactory<MessageHandler<TestMessage>>(new RecordingMessageHandler());
            var (serviceBusMessageHandlerBackgroundService, client) = Build(scopeFactory: scopeFactory);

            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            await processor.FireProcessMessage(new FakeReceivedMessage<TestMessage>(new TestMessage("a")));
            await processor.FireProcessMessage(new FakeReceivedMessage<TestMessage>(new TestMessage("b")));

            await serviceBusMessageHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(2, scopeFactory.ScopesCreated);
        }
    }

    public class OnProcessError
    {
        [Fact]
        public async Task DoesNotThrow()
        {
            var (serviceBusMessageHandlerBackgroundService, client) = Build();

            await serviceBusMessageHandlerBackgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var exception = await Record.ExceptionAsync(() =>
                processor.FireProcessError(new ErrorDetails(new Exception("boom"), "endpoint")));

            await serviceBusMessageHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Null(exception);
        }
    }

    private static (ServiceBusMessageHandlerBackgroundService<TestMessage> serviceBusMessageHandlerBackgroundService, FakeMessageBusClient client) Build(
        string queueName = "test-queue",
        bool readAndDelete = false,
        int maxConcurrentCalls = 1,
        RecordingMessageHandler? handler = null,
        FakeServiceScopeFactory<MessageHandler<TestMessage>>? scopeFactory = null)
    {
        var client = new FakeMessageBusClient();
        var settings = new HandlerSettings<TestMessage>(queueName, "TestHandler", readAndDelete, maxConcurrentCalls);
        var factory = scopeFactory ?? new FakeServiceScopeFactory<MessageHandler<TestMessage>>(handler ?? new RecordingMessageHandler());
        var serviceBusMessageHandlerBackgroundService = new ServiceBusMessageHandlerBackgroundService<TestMessage>(
            client, factory, settings, NullLogger<ServiceBusMessageHandlerBackgroundService<TestMessage>>.Instance, new HandlerInstrumentation(false));
        return (serviceBusMessageHandlerBackgroundService, client);
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

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            var processor = new FakeMessageBusProcessor<TMessage>(queueName, options);
            _processorCreated.SetResult((FakeMessageBusProcessor<TestMessage>)(object)processor);
            return Task.FromResult<IMessageBusProcessor<TMessage>>(processor);
        }

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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

        public Task StopProcessing(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task FireProcessMessage(IReceivedMessage<TMessage> message) =>
            ProcessMessage?.Invoke(message, CancellationToken.None) ?? Task.CompletedTask;

        public Task FireProcessError(ErrorDetails error) =>
            ProcessError?.Invoke(error) ?? Task.CompletedTask;
    }
}
