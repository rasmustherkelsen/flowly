using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class MessageStreamProcessingBackgroundServiceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private static (MessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler> BackgroundService, FakeStreamProcessor Processor, SpyStreamHandler Handler, SpyHandlerInstrumentation Instrumentation)
        CreateService(int maxMessages, int maxRetries, Func<IMessageStreamContext<TestMessage>, Task>? onHandle = null)
    {
        var processor = new FakeStreamProcessor();
        var client = new FakeStreamClient(processor);
        var registry = new MessageBusClientRegistry();
        registry.Register("primary", client, null);

        var handler = new SpyStreamHandler(onHandle);
        var services = new ServiceCollection();
        services.AddScoped<SpyStreamHandler>(_ => handler);
        var serviceProvider = services.BuildServiceProvider();

        var instrumentation = new SpyHandlerInstrumentation();

        var backgroundService = new MessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>(
            registry,
            new MessageStreamHandlerSettings<TestMessage, SpyStreamHandler>(
                "telemetry-reading",
                "primary",
                nameof(SpyStreamHandler),
                StartPosition.First(),
                maxMessages,
                TimeSpan.FromSeconds(30),
                maxRetries,
                0),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>>.Instance,
            instrumentation);

        return (backgroundService, processor, handler, instrumentation);
    }

    public class ExecuteAsync
    {
        [Fact]
        public async Task WhenClientIsNotStreamCapable_Throws()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("primary", new NonStreamClient(), null);

            var backgroundService = new MessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>(
                registry,
                new MessageStreamHandlerSettings<TestMessage, SpyStreamHandler>("q", "primary", "H", StartPosition.First(), 1, TimeSpan.FromSeconds(1), 0, 0),
                new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                NullLogger<MessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>>.Instance,
                new SpyHandlerInstrumentation());

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await backgroundService.StartAsync(CancellationToken.None);
                await backgroundService.ExecuteTask!.WaitAsync(TestTimeout);
            });
        }

        [Fact]
        public async Task CreatesStreamProcessorWithConfiguredStartPosition()
        {
            var (backgroundService, processor, _, _) = CreateService(maxMessages: 1, maxRetries: 0);

            await backgroundService.StartAsync(CancellationToken.None);
            await processor.Started.Task.WaitAsync(TestTimeout);

            Assert.Equal("telemetry-reading", processor.Client!.ReceivedQueueName);
            Assert.Equal(StartPosition.First(), processor.Client.ReceivedStartPosition);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task PrefetchIsSizedToMaxMessagesBeforeProcessing()
        {
            var (backgroundService, processor, _, _) = CreateService(maxMessages: 42, maxRetries: 0);

            await backgroundService.StartAsync(CancellationToken.None);
            await processor.Started.Task.WaitAsync(TestTimeout);

            Assert.Equal(42, processor.Client!.ReceivedOptions!.MaxConcurrentCalls);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task OnSuccess_CompletesAllMessagesAndRecordsSucceeded()
        {
            var (backgroundService, processor, handler, instrumentation) = CreateService(maxMessages: 2, maxRetries: 0);

            await backgroundService.StartAsync(CancellationToken.None);
            await processor.Started.Task.WaitAsync(TestTimeout);

            var first = new FakeReceivedMessage(new TestMessage("a"));
            var second = new FakeReceivedMessage(new TestMessage("b"));
            await processor.Deliver(first);
            await processor.Deliver(second);

            await handler.WaitForInvocations(1, TestTimeout);
            await first.Completed.Task.WaitAsync(TestTimeout);
            await second.Completed.Task.WaitAsync(TestTimeout);

            Assert.Equal([2L], instrumentation.SucceededCounts);
            Assert.Empty(instrumentation.HaltedReasons);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task OnFailure_RetriesSameBatchInProcessWithoutRepublishing()
        {
            var attempts = 0;
            var (backgroundService, processor, handler, instrumentation) = CreateService(
                maxMessages: 1,
                maxRetries: 2,
                onHandle: _ => ++attempts < 3 ? throw new InvalidOperationException("boom") : Task.CompletedTask);

            await backgroundService.StartAsync(CancellationToken.None);
            await processor.Started.Task.WaitAsync(TestTimeout);

            var message = new FakeReceivedMessage(new TestMessage("a"));
            await processor.Deliver(message);

            await handler.WaitForInvocations(3, TestTimeout);
            await message.Completed.Task.WaitAsync(TestTimeout);

            Assert.Equal(3, handler.Invocations.Count);
            Assert.All(handler.Invocations, messages => Assert.Equal("a", Assert.Single(messages).Payload));
            Assert.Equal(2, instrumentation.RetriedCounts.Count);
            Assert.False(processor.Client!.MessageBusSenderRequested);
            Assert.Empty(instrumentation.HaltedReasons);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenRetriesExhausted_HaltsWithoutCompletingMessages()
        {
            var (backgroundService, processor, handler, instrumentation) = CreateService(
                maxMessages: 1,
                maxRetries: 1,
                onHandle: _ => throw new InvalidOperationException("permanent failure"));

            await backgroundService.StartAsync(CancellationToken.None);
            await processor.Started.Task.WaitAsync(TestTimeout);

            var message = new FakeReceivedMessage(new TestMessage("a"));
            await processor.Deliver(message);

            await backgroundService.ExecuteTask!.WaitAsync(TestTimeout);

            Assert.Equal(2, handler.Invocations.Count);
            Assert.False(message.Completed.Task.IsCompleted);
            Assert.Equal(["permanent failure"], instrumentation.HaltedReasons);
            Assert.True(processor.StopProcessingCalled);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task AfterHalt_ProcessesNoFurtherMessages()
        {
            var (backgroundService, processor, handler, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: _ => throw new InvalidOperationException("permanent failure"));

            await backgroundService.StartAsync(CancellationToken.None);
            await processor.Started.Task.WaitAsync(TestTimeout);

            await processor.Deliver(new FakeReceivedMessage(new TestMessage("a")));
            await backgroundService.ExecuteTask!.WaitAsync(TestTimeout);

            await processor.Deliver(new FakeReceivedMessage(new TestMessage("b")));

            Assert.Single(handler.Invocations);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenTwoBackgroundServicesRegisteredForDifferentHandlerTypes_EachResolvesItsOwnHandlerIndependently()
        {
            var processorOne = new FakeStreamProcessor();
            var clientOne = new FakeStreamClient(processorOne);
            var processorTwo = new FakeStreamProcessor();
            var clientTwo = new FakeStreamClient(processorTwo);

            var registry = new MessageBusClientRegistry();
            registry.Register("primary-one", clientOne, null);
            registry.Register("primary-two", clientTwo, null);

            var handlerOne = new SpyStreamHandler(null);
            var handlerTwo = new SpyStreamHandlerTwo(null);

            var services = new ServiceCollection();
            services.AddScoped<SpyStreamHandler>(_ => handlerOne);
            services.AddScoped<SpyStreamHandlerTwo>(_ => handlerTwo);
            var serviceProvider = services.BuildServiceProvider();

            var backgroundServiceOne = new MessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>(
                registry,
                new MessageStreamHandlerSettings<TestMessage, SpyStreamHandler>(
                    "telemetry-reading", "primary-one", nameof(SpyStreamHandler), StartPosition.First(), 1, TimeSpan.FromSeconds(30), 0, 0),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<MessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>>.Instance,
                new SpyHandlerInstrumentation());

            var backgroundServiceTwo = new MessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandlerTwo>(
                registry,
                new MessageStreamHandlerSettings<TestMessage, SpyStreamHandlerTwo>(
                    "telemetry-reading", "primary-two", nameof(SpyStreamHandlerTwo), StartPosition.First(), 1, TimeSpan.FromSeconds(30), 0, 0),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<MessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandlerTwo>>.Instance,
                new SpyHandlerInstrumentation());

            await backgroundServiceOne.StartAsync(CancellationToken.None);
            await backgroundServiceTwo.StartAsync(CancellationToken.None);
            await processorOne.Started.Task.WaitAsync(TestTimeout);
            await processorTwo.Started.Task.WaitAsync(TestTimeout);

            await processorOne.Deliver(new FakeReceivedMessage(new TestMessage("a")));
            await handlerOne.WaitForInvocations(1, TestTimeout);

            await processorTwo.Deliver(new FakeReceivedMessage(new TestMessage("b")));
            await handlerTwo.WaitForInvocations(1, TestTimeout);

            Assert.Equal("a", Assert.Single(Assert.Single(handlerOne.Invocations)).Payload);
            Assert.Equal("b", Assert.Single(Assert.Single(handlerTwo.Invocations)).Payload);

            await backgroundServiceOne.StopAsync(CancellationToken.None);
            await backgroundServiceTwo.StopAsync(CancellationToken.None);
        }
    }

    private record TestMessage(string Payload);

    private sealed class SpyStreamHandler(Func<IMessageStreamContext<TestMessage>, Task>? onHandle) : MessageStreamHandler<TestMessage>
    {
        private readonly Lock _lock = new();
        private readonly List<TaskCompletionSource> _waiters = [];

        public List<IReadOnlyCollection<TestMessage>> Invocations { get; } = [];

        public override async Task Handle(IMessageStreamContext<TestMessage> messageContext)
        {
            List<TaskCompletionSource> toSignal;
            lock (_lock)
            {
                Invocations.Add(messageContext.Messages);
                toSignal = [.._waiters];
            }

            foreach (var waiter in toSignal)
                waiter.TrySetResult();

            if (onHandle != null)
                await onHandle(messageContext);
        }

        public async Task WaitForInvocations(int count, TimeSpan timeout)
        {
            while (true)
            {
                TaskCompletionSource waiter;
                lock (_lock)
                {
                    if (Invocations.Count >= count) return;
                    waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _waiters.Add(waiter);
                }

                await waiter.Task.WaitAsync(timeout);
            }
        }
    }

    private sealed class SpyStreamHandlerTwo(Func<IMessageStreamContext<TestMessage>, Task>? onHandle) : MessageStreamHandler<TestMessage>
    {
        private readonly Lock _lock = new();
        private readonly List<TaskCompletionSource> _waiters = [];

        public List<IReadOnlyCollection<TestMessage>> Invocations { get; } = [];

        public override async Task Handle(IMessageStreamContext<TestMessage> messageContext)
        {
            List<TaskCompletionSource> toSignal;
            lock (_lock)
            {
                Invocations.Add(messageContext.Messages);
                toSignal = [.._waiters];
            }

            foreach (var waiter in toSignal)
                waiter.TrySetResult();

            if (onHandle != null)
                await onHandle(messageContext);
        }

        public async Task WaitForInvocations(int count, TimeSpan timeout)
        {
            while (true)
            {
                TaskCompletionSource waiter;
                lock (_lock)
                {
                    if (Invocations.Count >= count) return;
                    waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _waiters.Add(waiter);
                }

                await waiter.Task.WaitAsync(timeout);
            }
        }
    }

    private sealed class FakeStreamClient(FakeStreamProcessor processor) : IMessageBusClient, IStreamCapableMessageBusClient
    {
        public string? ReceivedQueueName { get; private set; }
        public StartPosition? ReceivedStartPosition { get; private set; }
        public MessageBusProcessorOptions? ReceivedOptions { get; private set; }
        public bool MessageBusSenderRequested { get; private set; }

        public string MessagingSystem => "fake-stream";

        public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
        {
            ReceivedQueueName = queueName;
            ReceivedStartPosition = startPosition;
            ReceivedOptions = options;
            processor.Client = this;
            return Task.FromResult((IMessageBusProcessor<TMessage>)(object)processor);
        }

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        {
            MessageBusSenderRequested = true;
            throw new NotImplementedException();
        }

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NonStreamClient : IMessageBusClient
    {
        public string MessagingSystem => "fake";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeStreamProcessor : IMessageBusProcessor<TestMessage>
    {
        private readonly List<Func<IReceivedMessage<TestMessage>, CancellationToken, Task>> _messageHandlers = [];

        public FakeStreamClient? Client { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool StopProcessingCalled { get; private set; }

        public event Func<IReceivedMessage<TestMessage>, CancellationToken, Task> ProcessMessage
        {
            add => _messageHandlers.Add(value);
            remove => _messageHandlers.Remove(value);
        }

        public event Func<ErrorDetails, Task> ProcessError
        {
            add { }
            remove { }
        }

        public Task StartProcessingMessages(CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return Task.CompletedTask;
        }

        public Task StopProcessing(CancellationToken cancellationToken)
        {
            StopProcessingCalled = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async Task Deliver(IReceivedMessage<TestMessage> receivedMessage)
        {
            foreach (var handler in _messageHandlers)
                await handler(receivedMessage, CancellationToken.None);
        }
    }

    private sealed class FakeReceivedMessage(TestMessage body) : IReceivedMessage<TestMessage>
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TestMessage Body { get; } = body;
        public MessageProperties Properties { get; } = new(Guid.NewGuid().ToString(), string.Empty);

        public Task Complete(CancellationToken cancellationToken = default)
        {
            Completed.TrySetResult();
            return Task.CompletedTask;
        }

        public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class SpyHandlerInstrumentation : IHandlerInstrumentation
    {
        public List<long> SucceededCounts { get; } = [];
        public List<long> RetriedCounts { get; } = [];
        public List<string> HaltedReasons { get; } = [];

        public bool IsEnabled => false;

        public System.Diagnostics.Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, System.Diagnostics.ActivityContext parentContext = default)
            => null;

        public System.Diagnostics.Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<System.Diagnostics.ActivityLink> links)
            => null;

        public void RecordReceived(string handlerName, string queueName, long count = 1) { }

        public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) => SucceededCounts.Add(count);

        public void RecordFailed(string handlerName, string queueName, long count = 1) { }

        public void RecordRetried(string handlerName, string queueName, long count = 1) => RetriedCounts.Add(count);

        public void RecordHalted(string handlerName, string queueName, string reason) => HaltedReasons.Add(reason);

        public System.Diagnostics.Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId) => null;

        public void RecordResponseSent(string callQueueName, double durationMs) { }

        public void RecordResponseFailed(string callQueueName) { }
    }
}
