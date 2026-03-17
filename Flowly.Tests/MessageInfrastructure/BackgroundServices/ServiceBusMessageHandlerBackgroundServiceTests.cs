using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class ServiceBusMessageHandlerBackgroundServiceTests
{
    public class ExecuteAsync
    {
        [Fact]
        public async Task CreatesProcessorWithQueueNameFromSettings()
        {
            var (sut, client) = Build("test-queue");

            await sut.StartAsync(CancellationToken.None);
            await client.CreatedProcessor!.Started;
            await sut.StopAsync(CancellationToken.None);

            Assert.Equal("test-queue", client.CreatedProcessor!.QueueName);
        }

        [Fact]
        public async Task CreatesProcessorWithMaxConcurrentCallsFromSettings()
        {
            var (sut, client) = Build("test-queue", maxConcurrentCalls: 3);

            await sut.StartAsync(CancellationToken.None);
            await client.CreatedProcessor!.Started;
            await sut.StopAsync(CancellationToken.None);

            Assert.Equal(3, client.CreatedProcessor!.Options.MaxConcurrentCalls);
        }

        [Fact]
        public async Task CreatesProcessorWithPeekLockModeWhenReadAndDeleteIsFalse()
        {
            var (sut, client) = Build("test-queue", readAndDelete: false);

            await sut.StartAsync(CancellationToken.None);
            await client.CreatedProcessor!.Started;
            await sut.StopAsync(CancellationToken.None);

            Assert.Equal(MessageBusReceiveMode.PeekLock, client.CreatedProcessor!.Options.ReceiveMode);
        }

        [Fact]
        public async Task CreatesProcessorWithReceiveAndDeleteModeWhenReadAndDeleteIsTrue()
        {
            var (sut, client) = Build("test-queue", readAndDelete: true);

            await sut.StartAsync(CancellationToken.None);
            await client.CreatedProcessor!.Started;
            await sut.StopAsync(CancellationToken.None);

            Assert.Equal(MessageBusReceiveMode.ReceiveAndDelete, client.CreatedProcessor!.Options.ReceiveMode);
        }

        [Fact]
        public async Task StartsProcessingMessages()
        {
            var (sut, client) = Build("test-queue");

            await sut.StartAsync(CancellationToken.None);
            await client.CreatedProcessor!.Started;
            await sut.StopAsync(CancellationToken.None);

            Assert.True(client.CreatedProcessor!.StartProcessingWasCalled);
        }
    }

    public class OnProcessMessage
    {
        [Fact]
        public async Task InvokesHandlerWithMessageBody()
        {
            var handler = new RecordingMessageHandler();
            var (sut, client) = Build(handler: handler);

            await sut.StartAsync(CancellationToken.None);
            await client.CreatedProcessor!.Started;

            var message = new TestMessage("hello");
            await client.CreatedProcessor!.FireProcessMessage(new FakeReceivedMessage<TestMessage>(message));

            await sut.StopAsync(CancellationToken.None);

            Assert.Equal(message, handler.ReceivedMessage);
        }

        [Fact]
        public async Task CreatesNewScopePerMessage()
        {
            var scopeFactory = new FakeServiceScopeFactory<MessageHandlerBase<TestMessage>>(new RecordingMessageHandler());
            var (sut, client) = Build(scopeFactory: scopeFactory);

            await sut.StartAsync(CancellationToken.None);
            await client.CreatedProcessor!.Started;

            await client.CreatedProcessor!.FireProcessMessage(new FakeReceivedMessage<TestMessage>(new TestMessage("a")));
            await client.CreatedProcessor!.FireProcessMessage(new FakeReceivedMessage<TestMessage>(new TestMessage("b")));

            await sut.StopAsync(CancellationToken.None);

            Assert.Equal(2, scopeFactory.ScopesCreated);
        }
    }

    public class OnProcessError
    {
        [Fact]
        public async Task DoesNotThrow()
        {
            var (sut, client) = Build();

            await sut.StartAsync(CancellationToken.None);
            await client.CreatedProcessor!.Started;

            var exception = await Record.ExceptionAsync(() =>
                client.CreatedProcessor!.FireProcessError(new ErrorDetails(new Exception("boom"), "endpoint")));

            await sut.StopAsync(CancellationToken.None);

            Assert.Null(exception);
        }
    }

    private static (ServiceBusMessageHandlerBackgroundService<TestMessage> sut, FakeMessageBusClient client) Build(
        string queueName = "test-queue",
        bool readAndDelete = false,
        int maxConcurrentCalls = 1,
        RecordingMessageHandler? handler = null,
        FakeServiceScopeFactory<MessageHandlerBase<TestMessage>>? scopeFactory = null)
    {
        var client = new FakeMessageBusClient();
        var settings = new HandlerSettings<TestMessage>(queueName, "TestHandler", readAndDelete, maxConcurrentCalls);
        var factory = scopeFactory ?? new FakeServiceScopeFactory<MessageHandlerBase<TestMessage>>(handler ?? new RecordingMessageHandler());
        var sut = new ServiceBusMessageHandlerBackgroundService<TestMessage>(
            client, factory, settings, NullLogger<ServiceBusMessageHandlerBackgroundService<TestMessage>>.Instance);
        return (sut, client);
    }

    private record TestMessage(string Value);

    private class RecordingMessageHandler : MessageHandlerBase<TestMessage>
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
        public FakeMessageBusProcessor<TestMessage>? CreatedProcessor { get; private set; }

        public IMessageBusProcessor<TMessage> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            var processor = new FakeMessageBusProcessor<TMessage>(queueName, options);
            CreatedProcessor = (FakeMessageBusProcessor<TestMessage>)(object)processor;
            return processor;
        }

        public IMessageBusReceiver CreateReceiver(string queueName) => throw new NotImplementedException();
        public IExecutionLaneProcessor CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public IMessageBusSender CreateMessageBusSender(string queueName) => throw new NotImplementedException();
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
