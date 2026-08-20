using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class PartitionedMessageStreamProcessingBackgroundServiceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private static (PartitionedMessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler> BackgroundService, FakePartitionedStreamConsumer Consumer, SpyStreamHandler Handler, SpyHandlerInstrumentation Instrumentation, FakeCheckpoint? Checkpoint)
        CreateService(int maxMessages, int maxRetries, Func<IMessageStreamContext<TestMessage>, Task>? onHandle = null, FakeCheckpoint? checkpoint = null)
    {
        var consumer = new FakePartitionedStreamConsumer();
        var client = new FakePartitionedStreamClient(consumer);
        var registry = new MessageBusClientRegistry();
        registry.Register("primary", client, null);

        var handler = new SpyStreamHandler(onHandle);
        var services = new ServiceCollection();
        services.AddScoped<SpyStreamHandler>(_ => handler);
        if (checkpoint != null)
            services.AddSingleton<MessageStreamCheckpoint<TestMessage>>(checkpoint);
        var serviceProvider = services.BuildServiceProvider();

        var instrumentation = new SpyHandlerInstrumentation();

        var backgroundService = new PartitionedMessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>(
            registry,
            new PartitionedMessageStreamHandlerSettings<TestMessage, SpyStreamHandler>(
                "telemetry-reading",
                "primary",
                nameof(SpyStreamHandler),
                nameof(SpyStreamHandler),
                3,
                StartPosition.First(),
                maxMessages,
                TimeSpan.FromSeconds(30),
                maxRetries,
                0),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PartitionedMessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>>.Instance,
            instrumentation);

        return (backgroundService, consumer, handler, instrumentation, checkpoint);
    }

    public class ExecuteAsync
    {
        [Fact]
        public async Task WhenClientIsNotPartitionedStreamCapable_Throws()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("primary", new NonPartitionedClient(), null);

            var backgroundService = new PartitionedMessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>(
                registry,
                new PartitionedMessageStreamHandlerSettings<TestMessage, SpyStreamHandler>("q", "primary", "H", "H", 3, StartPosition.First(), 1, TimeSpan.FromSeconds(1), 0, 0),
                new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                NullLogger<PartitionedMessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>>.Instance,
                new SpyHandlerInstrumentation());

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await backgroundService.StartAsync(CancellationToken.None);
                await backgroundService.ExecuteTask!.WaitAsync(TestTimeout);
            });
        }

        [Fact]
        public async Task CreatesPartitionedStreamConsumerWithConfiguredPartitionCount()
        {
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            Assert.Equal("telemetry-reading", consumer.Client!.ReceivedQueueName);
            Assert.Equal(3, consumer.Client.ReceivedPartitionCount);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task OnPartitionAssigned_ProcessesThatPartitionIndependently()
        {
            var (backgroundService, consumer, handler, instrumentation, _) = CreateService(maxMessages: 1, maxRetries: 0);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var processor = await consumer.AssignPartition(0);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("a")));

            await handler.WaitForInvocations(1, TestTimeout);

            Assert.Equal(0, handler.Invocations.Single().Partition);
            Assert.Equal([1L], instrumentation.SucceededCounts);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task TwoPartitionsAssigned_ProcessIndependentlyWithoutMixingBatches()
        {
            var (backgroundService, consumer, handler, _, _) = CreateService(maxMessages: 1, maxRetries: 0);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var processorZero = await consumer.AssignPartition(0);
            var processorOne = await consumer.AssignPartition(1);

            await processorZero.Deliver(new FakeReceivedMessage(new TestMessage("from-zero")));
            await processorOne.Deliver(new FakeReceivedMessage(new TestMessage("from-one")));

            await handler.WaitForInvocations(2, TestTimeout);

            Assert.Contains(handler.Invocations, i => i.Partition == 0 && i.Messages.Single().Payload == "from-zero");
            Assert.Contains(handler.Invocations, i => i.Partition == 1 && i.Messages.Single().Payload == "from-one");

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task OnPartitionRevoked_StopsThatPartitionButNotOthers()
        {
            var (backgroundService, consumer, handler, _, _) = CreateService(maxMessages: 1, maxRetries: 0);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var processorZero = await consumer.AssignPartition(0);
            var processorOne = await consumer.AssignPartition(1);

            await consumer.RevokePartition(0);
            await processorZero.Deliver(new FakeReceivedMessage(new TestMessage("should-not-arrive")));

            await processorOne.Deliver(new FakeReceivedMessage(new TestMessage("still-works")));
            await handler.WaitForInvocations(1, TestTimeout);

            Assert.Single(handler.Invocations);
            Assert.Equal(1, handler.Invocations.Single().Partition);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenOnePartitionHalts_OtherPartitionKeepsRunning()
        {
            var (backgroundService, consumer, handler, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: ctx => ctx.Partition == 0 ? throw new InvalidOperationException("permanent failure") : Task.CompletedTask);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var processorZero = await consumer.AssignPartition(0);
            var processorOne = await consumer.AssignPartition(1);

            await processorZero.Deliver(new FakeReceivedMessage(new TestMessage("poison")));
            await Task.Delay(100);
            Assert.Contains("permanent failure", instrumentation.HaltedReasons);

            await processorOne.Deliver(new FakeReceivedMessage(new TestMessage("still-works")));
            await handler.WaitForInvocations(2, TestTimeout);

            Assert.Contains(handler.Invocations, i => i.Partition == 1 && i.Messages.Single().Payload == "still-works");

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenPartitionHalts_DisposesItsProcessor()
        {
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: _ => throw new InvalidOperationException("permanent failure"));

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var processor = await consumer.AssignPartition(0);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("poison")));

            await WaitUntil(() => instrumentation.HaltedReasons.Count == 1, TestTimeout);
            await WaitUntil(() => processor.DisposeAsyncCallCount == 1, TestTimeout);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenPartitionIsReassignedAfterRevoke_DoesNotReinitializeCheckpoint()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 9 };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await consumer.AssignPartition(2);
            await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);
            await consumer.RevokePartition(2);

            await consumer.AssignPartition(2);
            await consumer.Client.ReceivedResolveStartPosition!(2, CancellationToken.None);

            Assert.Single(checkpoint.InitializeCalls, c => c.Partition == 2);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ResolveStartPositionIsCalledPerPartitionWithConsumerNameAndPartition()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 9 };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var startPosition = await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);

            Assert.Equal(StartPosition.Offset(10), startPosition);
            Assert.Contains(checkpoint.InitializeCalls, c => c.ConsumerName == nameof(SpyStreamHandler) && c.Partition == 2);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task OnSuccess_SavesCheckpointWithPartitionAndOffset()
        {
            var checkpoint = new FakeCheckpoint();
            var (backgroundService, consumer, handler, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var processor = await consumer.AssignPartition(1);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("a"), streamOffset: 55));

            await handler.WaitForInvocations(1, TestTimeout);
            await WaitUntil(() => checkpoint.SaveCalls.Count == 1, TestTimeout);

            Assert.Equal(1, checkpoint.SaveCalls[0].Partition);
            Assert.Equal(55, checkpoint.SaveCalls[0].Position);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.True(condition(), "Condition was not met within timeout.");
        }
    }

    private record TestMessage(string Payload);

    private sealed record Invocation(int? Partition, IReadOnlyCollection<TestMessage> Messages);

    private sealed class SpyStreamHandler(Func<IMessageStreamContext<TestMessage>, Task>? onHandle) : MessageStreamHandler<TestMessage>
    {
        private readonly Lock _lock = new();
        private readonly List<TaskCompletionSource> _waiters = [];

        public List<Invocation> Invocations { get; } = [];

        public override async Task Handle(IMessageStreamContext<TestMessage> messageContext)
        {
            List<TaskCompletionSource> toSignal;
            lock (_lock)
            {
                Invocations.Add(new Invocation(messageContext.Partition, messageContext.Messages));
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

    private sealed class FakePartitionedStreamClient(FakePartitionedStreamConsumer consumer) : IMessageBusClient, IPartitionedStreamCapableMessageBusClient
    {
        public string? ReceivedQueueName { get; private set; }
        public int ReceivedPartitionCount { get; private set; }
        public Func<int, CancellationToken, Task<StartPosition>>? ReceivedResolveStartPosition { get; private set; }

        public string MessagingSystem => "fake-partitioned-stream";

        public Task<IPartitionedStreamConsumer<TMessage>> CreatePartitionedStreamConsumer<TMessage>(
            string queueName, int partitionCount, Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition, MessageBusProcessorOptions options,
            ILogger logger)
        {
            ReceivedQueueName = queueName;
            ReceivedPartitionCount = partitionCount;
            ReceivedResolveStartPosition = resolveStartPosition;
            consumer.Client = this;
            return Task.FromResult((IPartitionedStreamConsumer<TMessage>)(object)consumer);
        }

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NonPartitionedClient : IMessageBusClient
    {
        public string MessagingSystem => "fake";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakePartitionedStreamConsumer : IPartitionedStreamConsumer<TestMessage>
    {
        public FakePartitionedStreamClient? Client { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool StopProcessingCalled { get; private set; }

        public event Func<int, IMessageBusProcessor<TestMessage>, Task>? PartitionAssigned;
        public event Func<int, Task>? PartitionRevoked;

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

        public async Task<FakePartitionProcessor> AssignPartition(int partition)
        {
            var processor = new FakePartitionProcessor();
            if (PartitionAssigned != null)
                await PartitionAssigned(partition, processor);

            return processor;
        }

        public async Task RevokePartition(int partition)
        {
            if (PartitionRevoked != null)
                await PartitionRevoked(partition);
        }
    }

    private sealed class FakePartitionProcessor : IMessageBusProcessor<TestMessage>
    {
        private readonly List<Func<IReceivedMessage<TestMessage>, CancellationToken, Task>> _messageHandlers = [];

        public int DisposeAsyncCallCount { get; private set; }

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

        public Task StartProcessingMessages(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopProcessing(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCallCount++;
            return ValueTask.CompletedTask;
        }

        public async Task Deliver(IReceivedMessage<TestMessage> receivedMessage)
        {
            foreach (var handler in _messageHandlers)
                await handler(receivedMessage, CancellationToken.None);
        }
    }

    private sealed class FakeCheckpoint : MessageStreamCheckpoint<TestMessage>
    {
        public long? StoredPosition { get; set; }
        public List<MessageStreamCheckpointContext> InitializeCalls { get; } = [];
        public List<MessageStreamCheckpointSaveContext> SaveCalls { get; } = [];

        protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
        {
            InitializeCalls.Add(context);
            return Task.CompletedTask;
        }

        protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
            => Task.FromResult(StoredPosition);

        protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
        {
            SaveCalls.Add(context);
            StoredPosition = context.Position;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReceivedMessage(TestMessage body, long? streamOffset = null) : IReceivedMessage<TestMessage>
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TestMessage Body { get; } = body;
        public MessageProperties Properties { get; } = new(Guid.NewGuid().ToString(), string.Empty, StreamOffset: streamOffset);

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
