using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class StreamPartitionRunnerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private static Harness Create(
        int maxMessages = 1,
        int maxRetries = 0,
        int? partition = null,
        Func<IMessageStreamContext<TestMessage>, int, Task>? onHandle = null,
        bool withCheckpoint = false,
        int failingSaves = 0)
    {
        var processor = new FakeProcessor();
        var stateRegistry = new StreamPartitionStateRegistry(TimeProvider.System);
        var resumeRegistry = new StreamPartitionResumeRegistry();
        var instrumentation = new RecordingHandlerInstrumentation();
        var handler = new SpyHandler(onHandle);
        var checkpoint = new FakeCheckpoint(failingSaves);
        var services = new ServiceCollection();
        services.AddScoped<SpyHandler>(_ => handler);
        if (withCheckpoint)
            services.AddSingleton<MessageStreamCheckpoint<TestMessage>>(checkpoint);
        var reporter = new StreamPartitionStateReporter(stateRegistry, instrumentation, nameof(SpyHandler), "orders", "OrderConsumer");

        var runner = new StreamPartitionRunner<TestMessage, SpyHandler>(
            processor,
            partition,
            nameof(SpyHandler),
            "orders",
            "OrderConsumer",
            maxMessages,
            TimeSpan.FromSeconds(30),
            maxRetries,
            0,
            "test",
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance,
            instrumentation,
            reporter.Running(partition),
            resumeRegistry);

        return new Harness(runner, processor, handler, checkpoint, instrumentation, stateRegistry, resumeRegistry, new StreamPartitionKey("orders", "OrderConsumer", partition));
    }

    public class Run
    {
        [Fact]
        public async Task WhenRetriesAreExhausted_HoldsTheBatchAndKeepsTheProcessor()
        {
            var harness = Create(onHandle: (_, _) => throw new InvalidOperationException("permanent failure"));
            var runTask = harness.Start();

            var message = new FakeReceivedMessage("a");
            await harness.Processor.Deliver(message);
            await harness.Instrumentation.NextHalt();

            Assert.False(runTask.IsCompleted);
            Assert.False(message.Completed.Task.IsCompleted);
            Assert.Equal(0, harness.Processor.StopProcessingCalls);
            Assert.Equal(0, harness.Processor.DisposeCalls);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenRetriesAreExhausted_RunsTheHandlerOncePerAttemptAndNoMore()
        {
            var harness = Create(maxRetries: 2, onHandle: (_, _) => throw new InvalidOperationException("permanent failure"));
            var runTask = harness.Start();

            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();

            Assert.Equal(3, harness.Handler.Invocations.Count);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenHalted_RecordsHaltedStateAndTheErrorBeforeSignallingTheHalt()
        {
            var harness = Create(onHandle: (_, _) => throw new InvalidOperationException("permanent failure"));
            var runTask = harness.Start();

            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();

            var status = harness.StateRegistry.Get(harness.Key)!;
            Assert.Equal(StreamPartitionState.Halted, status.State);
            Assert.Equal("permanent failure", status.LastError!.Message);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenHalted_DoesNotSaveTheCheckpoint()
        {
            var harness = Create(withCheckpoint: true, onHandle: (_, _) => throw new InvalidOperationException("permanent failure"));
            var runTask = harness.Start();

            await harness.Processor.Deliver(new FakeReceivedMessage("a", streamOffset: 7));
            await harness.Instrumentation.NextHalt();

            Assert.Empty(harness.Checkpoint.SaveCalls);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenTheCheckpointSaveFailsOnce_RetriesOnlyTheSaveAndNotTheHandler()
        {
            var harness = Create(maxRetries: 1, withCheckpoint: true, failingSaves: 1);
            var runTask = harness.Start();
            var message = new FakeReceivedMessage("a", streamOffset: 7);

            await harness.Processor.Deliver(message);
            await harness.Checkpoint.WaitForSaves(2);

            Assert.Single(harness.Handler.Invocations);
            Assert.Equal(StreamPartitionState.Running, harness.StateRegistry.Get(harness.Key)!.State);
            Assert.Equal(0, harness.Instrumentation.HaltCount);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenTheCheckpointSaveKeepsFailing_HaltsWithoutRunningTheHandlerAgain()
        {
            var harness = Create(maxRetries: 2, withCheckpoint: true, failingSaves: int.MaxValue);
            var runTask = harness.Start();

            await harness.Processor.Deliver(new FakeReceivedMessage("a", streamOffset: 7));
            await harness.Instrumentation.NextHalt();

            var status = harness.StateRegistry.Get(harness.Key)!;
            Assert.Single(harness.Handler.Invocations);
            Assert.Equal(3, harness.Checkpoint.SaveCalls.Count);
            Assert.Equal(StreamPartitionState.Halted, status.State);
            Assert.Contains("checkpoint save", status.LastError!.Message);
            Assert.Contains("handler succeeded", status.LastError.Message);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenResumedAfterTheCheckpointSaveKeptFailing_SavesTheCheckpointWithoutRunningTheHandlerAgain()
        {
            var harness = Create(withCheckpoint: true, failingSaves: 1);
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a", streamOffset: 7));
            await harness.Instrumentation.NextHalt();

            harness.ResumeRegistry.Resume(harness.Key);
            await harness.Checkpoint.WaitForSaves(2);
            await harness.Instrumentation.NextSuccess();

            Assert.Single(harness.Handler.Invocations);
            Assert.Equal(7, harness.Checkpoint.SaveCalls[^1].Position);
            Assert.Equal(StreamPartitionState.Running, harness.StateRegistry.Get(harness.Key)!.State);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenCompletingTheMessageFailsOnce_RetriesOnlyTheCompletionAndNotTheHandler()
        {
            var harness = Create(maxRetries: 1, withCheckpoint: true);
            var runTask = harness.Start();
            var message = new FakeReceivedMessage("a", streamOffset: 7, failingCompletes: 1);

            await harness.Processor.Deliver(message);
            await message.Completed.Task.WaitAsync(TestTimeout);
            await harness.Checkpoint.WaitForSaves(1);

            Assert.Single(harness.Handler.Invocations);
            Assert.Equal(2, message.CompleteCalls);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenCompletingTheMessageKeepsFailing_HaltsWithoutSavingTheCheckpointOrRunningTheHandlerAgain()
        {
            var harness = Create(withCheckpoint: true);
            var runTask = harness.Start();

            await harness.Processor.Deliver(new FakeReceivedMessage("a", streamOffset: 7, failingCompletes: int.MaxValue));
            await harness.Instrumentation.NextHalt();

            var status = harness.StateRegistry.Get(harness.Key)!;
            Assert.Single(harness.Handler.Invocations);
            Assert.Empty(harness.Checkpoint.SaveCalls);
            Assert.Equal(StreamPartitionState.Halted, status.State);
            Assert.Contains("completion", status.LastError!.Message);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenResumedAfterCompletionKeptFailing_CompletesOnlyTheMessagesNotCompletedYet()
        {
            var harness = Create(maxMessages: 2, withCheckpoint: true);
            var runTask = harness.Start();
            var first = new FakeReceivedMessage("a", streamOffset: 1);
            var second = new FakeReceivedMessage("b", streamOffset: 2, failingCompletes: 1);
            await harness.Processor.Deliver(first);
            await harness.Processor.Deliver(second);
            await harness.Instrumentation.NextHalt();

            harness.ResumeRegistry.Resume(harness.Key);
            await harness.Checkpoint.WaitForSaves(1);

            Assert.Single(harness.Handler.Invocations);
            Assert.Equal(1, first.CompleteCalls);
            Assert.Equal(2, second.CompleteCalls);
            Assert.Equal(2, harness.Checkpoint.SaveCalls[0].Position);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenHaltedAfterTheHandlerSucceeded_BlocksFurtherMessagesFromBeingHandled()
        {
            var harness = Create(withCheckpoint: true, failingSaves: int.MaxValue);
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a", streamOffset: 1));
            await harness.Instrumentation.NextHalt();

            await harness.Processor.Deliver(new FakeReceivedMessage("b", streamOffset: 2));
            var blocked = harness.Processor.Deliver(new FakeReceivedMessage("c", streamOffset: 3));

            Assert.False(blocked.IsCompleted);
            Assert.Single(harness.Handler.Invocations);

            await harness.Stop(runTask);
            await blocked.WaitAsync(TestTimeout);
        }

        [Fact]
        public async Task WhenResumedAfterTheCheckpointSaveKeptFailing_HandlesTheBufferedBatchAfterTheHeldOne()
        {
            var harness = Create(withCheckpoint: true, failingSaves: 1);
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a", streamOffset: 1));
            await harness.Instrumentation.NextHalt();
            await harness.Processor.Deliver(new FakeReceivedMessage("b", streamOffset: 2));

            harness.ResumeRegistry.Resume(harness.Key);
            await harness.Handler.WaitForInvocations(2);

            Assert.Equal(["a", "b"], harness.Handler.Invocations.SelectMany(invocation => invocation).Select(message => message.Payload));

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenHalted_BlocksDeliveryOnceTheBufferIsFull()
        {
            var harness = Create(maxMessages: 2, onHandle: (_, _) => throw new InvalidOperationException("permanent failure"));
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Processor.Deliver(new FakeReceivedMessage("b"));
            await harness.Instrumentation.NextHalt();

            await harness.Processor.Deliver(new FakeReceivedMessage("c"));
            await harness.Processor.Deliver(new FakeReceivedMessage("d"));
            var blocked = harness.Processor.Deliver(new FakeReceivedMessage("e"));

            Assert.False(blocked.IsCompleted);

            await harness.Stop(runTask);
            await blocked.WaitAsync(TestTimeout);
        }

        [Fact]
        public async Task WhenHalted_KeepsTheBlockedDeliveryPendingWithoutHandlingFurtherMessages()
        {
            var harness = Create(onHandle: (_, _) => throw new InvalidOperationException("permanent failure"));
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();
            await harness.Processor.Deliver(new FakeReceivedMessage("b"));
            var blocked = harness.Processor.Deliver(new FakeReceivedMessage("c"));

            Assert.False(blocked.IsCompleted);
            Assert.Single(harness.Handler.Invocations);

            await harness.Stop(runTask);
            await blocked.WaitAsync(TestTimeout);
        }

        [Fact]
        public async Task WhenResumed_RunsTheHeldBatchAgainAndThenTheBufferedMessagesInOrder()
        {
            var harness = Create(onHandle: (_, attempt) => attempt == 1 ? throw new InvalidOperationException("transient") : Task.CompletedTask);
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();
            await harness.Processor.Deliver(new FakeReceivedMessage("b"));
            var blocked = harness.Processor.Deliver(new FakeReceivedMessage("c"));

            harness.ResumeRegistry.Resume(harness.Key);
            await blocked.WaitAsync(TestTimeout);
            await harness.Handler.WaitForInvocations(4);

            Assert.Equal(["a", "a", "b", "c"], harness.Handler.Invocations.SelectMany(invocation => invocation).Select(message => message.Payload));

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenResumed_CompletesAndCheckpointsTheHeldBatch()
        {
            var harness = Create(withCheckpoint: true, onHandle: (_, attempt) => attempt == 1 ? throw new InvalidOperationException("transient") : Task.CompletedTask);
            var runTask = harness.Start();
            var message = new FakeReceivedMessage("a", streamOffset: 7);
            await harness.Processor.Deliver(message);
            await harness.Instrumentation.NextHalt();

            harness.ResumeRegistry.Resume(harness.Key);
            await message.Completed.Task.WaitAsync(TestTimeout);
            await harness.Checkpoint.WaitForSaves(1);

            var save = Assert.Single(harness.Checkpoint.SaveCalls);
            Assert.Equal(7, save.Position);
            Assert.Equal("OrderConsumer", save.ConsumerName);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenResumed_CountsTheHeldBatchAsReceivedOnlyOnce()
        {
            var harness = Create(onHandle: (_, attempt) => attempt == 1 ? throw new InvalidOperationException("transient") : Task.CompletedTask);
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();

            harness.ResumeRegistry.Resume(harness.Key);
            await harness.Handler.WaitForInvocations(2);

            Assert.Equal([1L], harness.Instrumentation.ReceivedCounts);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenResumedBatchFailsAgain_HaltsAgainAfterAFreshSetOfRetries()
        {
            var harness = Create(maxRetries: 1, onHandle: (_, _) => throw new InvalidOperationException("permanent failure"));
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();

            harness.ResumeRegistry.Resume(harness.Key);
            await harness.Instrumentation.NextHalt();

            Assert.Equal(4, harness.Handler.Invocations.Count);
            Assert.Equal(StreamPartitionState.Halted, harness.StateRegistry.Get(harness.Key)!.State);
            Assert.False(runTask.IsCompleted);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenResumedBatchFailsAgain_CanBeResumedAgain()
        {
            var harness = Create(onHandle: (_, attempt) => attempt <= 2 ? throw new InvalidOperationException("transient") : Task.CompletedTask);
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();
            harness.ResumeRegistry.Resume(harness.Key);
            await harness.Instrumentation.NextHalt();

            var result = harness.ResumeRegistry.Resume(harness.Key);
            await harness.Handler.WaitForInvocations(3);

            Assert.Equal(StreamPartitionResumeResult.Resumed, result);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenCancelledWhileHeld_CompletesAndReleasesTheBlockedDelivery()
        {
            var harness = Create(onHandle: (_, _) => throw new InvalidOperationException("permanent failure"));
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();
            await harness.Processor.Deliver(new FakeReceivedMessage("b"));
            var blocked = harness.Processor.Deliver(new FakeReceivedMessage("c"));

            await harness.Stop(runTask);
            await blocked.WaitAsync(TestTimeout);

            Assert.True(runTask.IsCompletedSuccessfully);
            Assert.Single(harness.Handler.Invocations);
        }

        [Fact]
        public async Task WhenTheTransportCancelsABlockedDelivery_PropagatesTheCancellation()
        {
            var harness = Create(onHandle: (_, _) => throw new InvalidOperationException("permanent failure"));
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();
            await harness.Processor.Deliver(new FakeReceivedMessage("b"));
            using var transportCts = new CancellationTokenSource();
            var blocked = harness.Processor.Deliver(new FakeReceivedMessage("c"), transportCts.Token);

            await transportCts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked.WaitAsync(TestTimeout));

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task ForAPartition_PassesThePartitionToTheHandlerWhenTheBatchIsRunAgain()
        {
            var harness = Create(partition: 3, onHandle: (_, attempt) => attempt == 1 ? throw new InvalidOperationException("transient") : Task.CompletedTask);
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();

            harness.ResumeRegistry.Resume(harness.Key);
            await harness.Handler.WaitForInvocations(2);

            Assert.All(harness.Handler.Partitions, partition => Assert.Equal(3, partition));

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhileRunning_IsReachableThroughTheResumeRegistry()
        {
            var harness = Create();
            var runTask = harness.Start();
            await harness.Processor.Started.Task.WaitAsync(TestTimeout);

            Assert.Equal(StreamPartitionResumeResult.NotHalted, harness.ResumeRegistry.Resume(harness.Key));

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task AfterItEnded_IsNoLongerReachableThroughTheResumeRegistry()
        {
            var harness = Create();
            var runTask = harness.Start();
            await harness.Processor.Started.Task.WaitAsync(TestTimeout);

            await harness.Stop(runTask);

            Assert.Equal(StreamPartitionResumeResult.NotOwned, harness.ResumeRegistry.Resume(harness.Key));
        }
    }

    public class Resume
    {
        [Fact]
        public async Task WhenNotHalted_ReturnsFalse()
        {
            var harness = Create();
            var runTask = harness.Start();
            await harness.Processor.Started.Task.WaitAsync(TestTimeout);

            Assert.False(harness.Runner.Resume());

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenHalted_ReturnsTrueAndMovesTheStateToRunningKeepingTheLastError()
        {
            var harness = Create(onHandle: (_, attempt) => attempt == 1 ? throw new InvalidOperationException("transient") : Task.CompletedTask);
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();

            var resumed = harness.Runner.Resume();

            var status = harness.StateRegistry.Get(harness.Key)!;
            Assert.True(resumed);
            Assert.Equal(StreamPartitionState.Running, status.State);
            Assert.Equal("transient", status.LastError!.Message);
            Assert.Equal(
                [StreamPartitionState.Running, StreamPartitionState.Halted, StreamPartitionState.Running],
                harness.Instrumentation.Transitions.Select(transition => transition.State));

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task WhenCalledTwiceForTheSameHalt_ResumesOnlyOnce()
        {
            var harness = Create(onHandle: (_, attempt) => attempt == 1 ? throw new InvalidOperationException("transient") : Task.CompletedTask);
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();

            var first = harness.Runner.Resume();
            var second = harness.Runner.Resume();
            await harness.Handler.WaitForInvocations(2);

            Assert.True(first);
            Assert.False(second);
            Assert.Equal(2, harness.Handler.Invocations.Count);

            await harness.Stop(runTask);
        }

        [Fact]
        public async Task AfterTheRunnerWasCancelled_ReturnsFalse()
        {
            var harness = Create(onHandle: (_, _) => throw new InvalidOperationException("permanent failure"));
            var runTask = harness.Start();
            await harness.Processor.Deliver(new FakeReceivedMessage("a"));
            await harness.Instrumentation.NextHalt();
            await harness.Stop(runTask);

            Assert.False(harness.Runner.Resume());
        }
    }

    private sealed record TestMessage(string Payload);

    private sealed class Harness(
        StreamPartitionRunner<TestMessage, SpyHandler> runner,
        FakeProcessor processor,
        SpyHandler handler,
        FakeCheckpoint checkpoint,
        RecordingHandlerInstrumentation instrumentation,
        StreamPartitionStateRegistry stateRegistry,
        StreamPartitionResumeRegistry resumeRegistry,
        StreamPartitionKey key)
    {
        private readonly CancellationTokenSource _cts = new();

        public StreamPartitionRunner<TestMessage, SpyHandler> Runner => runner;
        public FakeProcessor Processor => processor;
        public SpyHandler Handler => handler;
        public FakeCheckpoint Checkpoint => checkpoint;
        public RecordingHandlerInstrumentation Instrumentation => instrumentation;
        public StreamPartitionStateRegistry StateRegistry => stateRegistry;
        public StreamPartitionResumeRegistry ResumeRegistry => resumeRegistry;
        public StreamPartitionKey Key => key;

        public Task Start() => runner.Run(_cts.Token);

        public async Task Stop(Task runTask)
        {
            await _cts.CancelAsync();
            await runTask.WaitAsync(TestTimeout);
        }
    }

    private sealed class SpyHandler(Func<IMessageStreamContext<TestMessage>, int, Task>? onHandle) : MessageStreamHandler<TestMessage>
    {
        private readonly Lock _lock = new();
        private readonly List<TaskCompletionSource> _waiters = [];
        private readonly List<IReadOnlyCollection<TestMessage>> _invocations = [];
        private readonly List<int?> _partitions = [];

        public IReadOnlyList<IReadOnlyCollection<TestMessage>> Invocations
        {
            get
            {
                lock (_lock)
                {
                    return [.._invocations];
                }
            }
        }

        public IReadOnlyList<int?> Partitions
        {
            get
            {
                lock (_lock)
                {
                    return [.._partitions];
                }
            }
        }

        public override async Task Handle(IMessageStreamContext<TestMessage> messageContext)
        {
            int attempt;
            List<TaskCompletionSource> toSignal;
            lock (_lock)
            {
                _invocations.Add(messageContext.Messages);
                _partitions.Add(messageContext.Partition);
                attempt = _invocations.Count;
                toSignal = [.._waiters];
            }

            foreach (var waiter in toSignal)
            {
                waiter.TrySetResult();
            }

            if (onHandle != null)
                await onHandle(messageContext, attempt);
        }

        public async Task WaitForInvocations(int count)
        {
            while (true)
            {
                TaskCompletionSource waiter;
                lock (_lock)
                {
                    if (_invocations.Count >= count) return;

                    waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _waiters.Add(waiter);
                }

                await waiter.Task.WaitAsync(TestTimeout);
            }
        }
    }

    private sealed class FakeProcessor : IMessageBusProcessor<TestMessage>
    {
        private readonly List<Func<IReceivedMessage<TestMessage>, CancellationToken, Task>> _messageHandlers = [];

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int StopProcessingCalls { get; private set; }
        public int DisposeCalls { get; private set; }

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
            StopProcessingCalls++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }

        public async Task Deliver(IReceivedMessage<TestMessage> receivedMessage, CancellationToken cancellationToken = default)
        {
            foreach (var handler in _messageHandlers)
            {
                await handler(receivedMessage, cancellationToken);
            }
        }
    }

    private sealed class FakeCheckpoint(int failingSaves) : MessageStreamCheckpoint<TestMessage>
    {
        private readonly Lock _lock = new();
        private readonly List<TaskCompletionSource> _waiters = [];
        private readonly List<MessageStreamCheckpointSaveContext> _saveCalls = [];

        public IReadOnlyList<MessageStreamCheckpointSaveContext> SaveCalls
        {
            get
            {
                lock (_lock)
                {
                    return [.._saveCalls];
                }
            }
        }

        public async Task WaitForSaves(int count)
        {
            while (true)
            {
                TaskCompletionSource waiter;
                lock (_lock)
                {
                    if (_saveCalls.Count >= count) return;

                    waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _waiters.Add(waiter);
                }

                await waiter.Task.WaitAsync(TestTimeout);
            }
        }

        protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken) => Task.FromResult<long?>(null);

        protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
        {
            List<TaskCompletionSource> toSignal;
            lock (_lock)
            {
                _saveCalls.Add(context);
                toSignal = [.._waiters];
            }

            foreach (var waiter in toSignal)
            {
                waiter.TrySetResult();
            }

            return _saveCalls.Count <= failingSaves ? Task.FromException(new InvalidOperationException("checkpoint store unavailable")) : Task.CompletedTask;
        }
    }

    private sealed class FakeReceivedMessage(string payload, long? streamOffset = null, int failingCompletes = 0) : IReceivedMessage<TestMessage>
    {
        public int CompleteCalls { get; private set; }

        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TestMessage Body { get; } = new(payload);
        public MessageProperties Properties { get; } = new(Guid.NewGuid().ToString(), string.Empty, StreamOffset: streamOffset);

        public Task Complete(CancellationToken cancellationToken = default)
        {
            CompleteCalls++;
            if (CompleteCalls <= failingCompletes)
                return Task.FromException(new InvalidOperationException("broker unavailable"));

            Completed.TrySetResult();
            return Task.CompletedTask;
        }

        public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
