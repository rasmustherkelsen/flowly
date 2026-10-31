using Flowly.MessageInfrastructure.StreamPartitions;
using Azure.Messaging.EventHubs.Consumer;
using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsStreamBackgroundServicesTests
{
    private const string ProviderName = "azure-event-hubs";
    private const string EventHubName = "readings";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private static readonly EventHubsPartitionState EmptyPartition = new(-1, -1, default, true);

    private static Harness CreateHarness(
        StartPosition? startPosition = null,
        EventHubsPartitionState? partitionState = null,
        bool withCheckpoint = false,
        int maxMessages = 1,
        int maxRetries = 0,
        Func<IMessageStreamContext<string>, Task>? onHandle = null,
        string consumerName = "ReadingsHandler")
    {
        var clientFactory = new FakeEventHubsClientFactory();
        var inspector = new FakeEventHubsInspector(["0"], partitionState: partitionState);
        clientFactory.Inspectors[EventHubName] = inspector;
        var processorFactory = new FakeEventHubsProcessorFactory();
        var registry = new MessageBusClientRegistry();
        registry.Register(ProviderName, TestAzureEventHubsClient.Create(clientFactory, processorFactory), null);

        var handler = new SpyStreamHandler(onHandle);
        var stateRegistry = new StreamPartitionStateRegistry(TimeProvider.System);
        var checkpoint = new FakeMessageStreamCheckpoint<string>();
        var services = new ServiceCollection();
        services.AddScoped<SpyStreamHandler>(_ => handler);
        if (withCheckpoint)
            services.AddSingleton<MessageStreamCheckpoint<string>>(checkpoint);
        var serviceProvider = services.BuildServiceProvider();

        var service = new MessageStreamProcessingBackgroundService<string, SpyStreamHandler>(
            registry,
            new MessageStreamHandlerSettings<string, SpyStreamHandler>(
                EventHubName,
                ProviderName,
                nameof(SpyStreamHandler),
                consumerName,
                startPosition ?? StartPosition.First(),
                maxMessages,
                TimeSpan.FromSeconds(30),
                maxRetries,
                0),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MessageStreamProcessingBackgroundService<string, SpyStreamHandler>>.Instance,
            new NullHandlerInstrumentation(),
                stateRegistry);

        return new Harness { StateRegistry = stateRegistry, ProcessorFactory = processorFactory, Inspector = inspector, Checkpoint = checkpoint, Handler = handler, Service = service };
    }

    private static PartitionedHarness CreatePartitionedHarness(
        StartPosition? startPosition = null,
        bool withCheckpoint = false,
        int partitionCount = 3,
        int maxRetries = 0,
        Func<IMessageStreamContext<string>, Task>? onHandle = null)
    {
        var clientFactory = new FakeEventHubsClientFactory();
        clientFactory.Inspectors[EventHubName] = new FakeEventHubsInspector([.. Enumerable.Range(0, partitionCount).Select(partition => partition.ToString())], partitionState: EmptyPartition);
        var processorFactory = new FakeEventHubsProcessorFactory();
        var registry = new MessageBusClientRegistry();
        registry.Register(ProviderName, TestAzureEventHubsClient.Create(clientFactory, processorFactory), null);

        var handler = new SpyStreamHandler(onHandle);
        var stateRegistry = new StreamPartitionStateRegistry(TimeProvider.System);
        var checkpoint = new FakeMessageStreamCheckpoint<string>();
        var services = new ServiceCollection();
        services.AddScoped<SpyStreamHandler>(_ => handler);
        if (withCheckpoint)
            services.AddSingleton<MessageStreamCheckpoint<string>>(checkpoint);
        var serviceProvider = services.BuildServiceProvider();

        var service = new PartitionedMessageStreamProcessingBackgroundService<string, SpyStreamHandler>(
            registry,
            new PartitionedMessageStreamHandlerSettings<string, SpyStreamHandler>(
                EventHubName,
                ProviderName,
                nameof(SpyStreamHandler),
                "ReadingsHandler",
                partitionCount,
                startPosition ?? StartPosition.First(),
                1,
                TimeSpan.FromSeconds(30),
                maxRetries,
                0),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PartitionedMessageStreamProcessingBackgroundService<string, SpyStreamHandler>>.Instance,
            new NullHandlerInstrumentation(),
                stateRegistry);

        return new PartitionedHarness { StateRegistry = stateRegistry, ProcessorFactory = processorFactory, Checkpoint = checkpoint, Handler = handler, Service = service };
    }

    private static async Task<Task> DeliverUntilBlocked(FakeEventHubsProcessor processor, string partitionId)
    {
        Task delivery;

        do
        {
            delivery = processor.Deliver(partitionId, TestEvents.Create(1000, "probe"));
            await Task.Yield();
        }
        while (delivery.IsCompleted);

        return delivery;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);

        while (!condition())
        {
            await Task.Yield();
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
    }

    public class NonPartitioned
    {
        [Fact]
        public async Task ReadsTheEventHubThroughTheConsumerGroupNamedByTheConsumerName()
        {
            var harness = CreateHarness(consumerName: "reporting-group");

            await harness.Start();

            Assert.Equal(EventHubName, harness.Processor.EventHubName);
            Assert.Equal("reporting-group", harness.Processor.ConsumerGroup);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task DeliversTheEventsToTheHandlerAsOneBatch()
        {
            var harness = CreateHarness(maxMessages: 2);
            await harness.Start();

            await harness.Processor.Deliver("0", TestEvents.Create(0, "a"), TestEvents.Create(1, "b"));
            await harness.Handler.WaitForInvocations(1);

            Assert.Equal(["a", "b"], harness.Handler.Invocations.Single().Messages);
            Assert.Null(harness.Handler.Invocations.Single().Partition);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WithoutCheckpoint_LastStartsAtLatest()
        {
            var harness = CreateHarness(StartPosition.Last());
            await harness.Start();

            Assert.Equal(EventPosition.Latest, await harness.Processor.Callbacks.GetStartingPosition("0", CancellationToken.None));
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WithAStoredCheckpoint_StartsAfterTheStoredSequenceNumber()
        {
            var harness = CreateHarness(StartPosition.First(), withCheckpoint: true);
            harness.Checkpoint.Store(null, 9);
            await harness.Start();

            Assert.Equal(EventPosition.FromSequenceNumber(9, isInclusive: false), await harness.Processor.Callbacks.GetStartingPosition("0", CancellationToken.None));
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WithAStoredCheckpoint_DoesNotAskTheEventHubForTheStartOffset()
        {
            var harness = CreateHarness(StartPosition.Last(), partitionState: EmptyPartition, withCheckpoint: true);
            harness.Checkpoint.Store(null, 9);
            await harness.Start();

            Assert.Empty(harness.Inspector.RequestedPartitionStates);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WithACheckpointAndLastOnAnEmptyPartition_SeedsMinusOneAndStartsAtTheBeginning()
        {
            var harness = CreateHarness(StartPosition.Last(), EmptyPartition, withCheckpoint: true);
            await harness.Start();

            Assert.Equal([(null, -1L, true)], harness.Checkpoint.Saves);
            Assert.Equal(EventPosition.FromSequenceNumber(-1, isInclusive: false), await harness.Processor.Callbacks.GetStartingPosition("0", CancellationToken.None));
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WithACheckpointAndLastOnAnEmptyPartition_ProcessesAndStoresTheFirstEventEverSent()
        {
            var harness = CreateHarness(StartPosition.Last(), EmptyPartition, withCheckpoint: true);
            await harness.Start();

            await harness.Processor.Deliver("0", TestEvents.Create(0, "first"));
            await WaitUntil(() => harness.Checkpoint.Saves.Count == 2);

            Assert.Equal(["first"], harness.Handler.Invocations.Single().Messages);
            Assert.Equal((null, 0L, false), harness.Checkpoint.Saves[1]);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WithACheckpointAndLastOnAPopulatedPartition_SeedsTheLastEnqueuedSequenceNumber()
        {
            var harness = CreateHarness(StartPosition.Last(), new EventHubsPartitionState(0, 29, DateTimeOffset.UtcNow, false), withCheckpoint: true);
            await harness.Start();

            Assert.Equal([(null, 29L, true)], harness.Checkpoint.Saves);
            Assert.Equal(EventPosition.FromSequenceNumber(29, isInclusive: false), await harness.Processor.Callbacks.GetStartingPosition("0", CancellationToken.None));
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WithACheckpointAndATimestampBeforeTheLastEvent_FallsBackToTheTimestampAndStoresAfterTheFirstBatch()
        {
            var lastEnqueuedTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
            var timestamp = lastEnqueuedTime.UtcDateTime.AddHours(-1);
            var harness = CreateHarness(StartPosition.Timestamp(timestamp), new EventHubsPartitionState(0, 29, lastEnqueuedTime, false), withCheckpoint: true);
            await harness.Start();

            Assert.Empty(harness.Checkpoint.Saves);
            Assert.Equal(EventPosition.FromEnqueuedTime(new DateTimeOffset(timestamp, TimeSpan.Zero)), await harness.Processor.Callbacks.GetStartingPosition("0", CancellationToken.None));

            await harness.Processor.Deliver("0", TestEvents.Create(25));
            await WaitUntil(() => harness.Checkpoint.Saves.Count == 1);

            Assert.Equal((null, 25L, false), harness.Checkpoint.Saves.Single());
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StoresTheSequenceNumberOfTheLastEventOfEachProcessedBatch()
        {
            var harness = CreateHarness(withCheckpoint: true, maxMessages: 2);
            await harness.Start();

            await harness.Processor.Deliver("0", TestEvents.Create(4, "a"), TestEvents.Create(5, "b"));
            await WaitUntil(() => harness.Checkpoint.Saves.Count == 1);

            Assert.Equal((null, 5L, false), harness.Checkpoint.Saves.Single());
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenRetriesAreExhausted_HaltsWithoutStoringAndKeepsOwnershipOfThePartition()
        {
            var harness = CreateHarness(withCheckpoint: true, onHandle: _ => throw new InvalidOperationException("handler failed"));
            await harness.Start();

            await harness.Processor.Deliver("0", TestEvents.Create(0, "poison"));
            await harness.Handler.WaitForInvocations(1);
            await harness.Service.ExecuteTask!.WaitAsync(TestTimeout);
            var blocked = harness.Processor.Deliver("0", TestEvents.Create(1, "after"));

            Assert.False(blocked.IsCompleted);
            Assert.Empty(harness.Checkpoint.Saves);
            Assert.Equal(0, harness.Processor.Stops);

            await harness.Service.StopAsync(CancellationToken.None);
            await blocked.WaitAsync(TestTimeout);

            Assert.Equal(1, harness.Processor.Stops);
            Assert.Single(harness.Handler.Invocations);
        }

        [Fact]
        public async Task RecordsTheSinglePartitionAsRunningAndRemovesItOnStop()
        {
            var harness = CreateHarness();
            await harness.Start();

            Assert.Equal(StreamPartitionState.Running, harness.StateRegistry.Get(new StreamPartitionKey(EventHubName, "ReadingsHandler", null))!.State);

            await harness.Service.StopAsync(CancellationToken.None);

            Assert.Empty(harness.StateRegistry.GetAll());
        }

        [Fact]
        public async Task WhenRetriesAreExhausted_RecordsTheSinglePartitionAsHaltedWithTheError()
        {
            var harness = CreateHarness(onHandle: _ => throw new InvalidOperationException("handler failed"));
            await harness.Start();

            await harness.Processor.Deliver("0", TestEvents.Create(0, "poison"));
            await harness.Service.ExecuteTask!.WaitAsync(TestTimeout);

            var status = harness.StateRegistry.Get(new StreamPartitionKey(EventHubName, "ReadingsHandler", null))!;
            Assert.Equal(StreamPartitionState.Halted, status.State);
            Assert.Equal("handler failed", status.LastError!.Message);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task OnStop_StopsTheSdkProcessor()
        {
            var harness = CreateHarness();
            await harness.Start();

            await harness.Service.StopAsync(CancellationToken.None);

            Assert.Equal(1, harness.Processor.Stops);
        }

        [Fact]
        public async Task WithAMissingConsumerGroup_FailsHostStartWithTheSdkProcessorsError()
        {
            var harness = CreateHarness();
            harness.ProcessorFactory.StartFailure = new InvalidOperationException("consumer group missing");

            await harness.Service.StartAsync(CancellationToken.None);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.ExecuteTask!.WaitAsync(TestTimeout));
            Assert.Equal("consumer group missing", exception.Message);
        }
    }

    public class Partitioned
    {
        [Fact]
        public async Task RunsOneLoopPerAssignedPartitionAndReportsThePartitionToTheHandler()
        {
            var harness = CreatePartitionedHarness();
            await harness.Start();
            await harness.Processor.Assign("0");
            await harness.Processor.Assign("2");

            await harness.Processor.Deliver("0", TestEvents.Create(0, "p0"));
            await harness.Processor.Deliver("2", TestEvents.Create(0, "p2"));
            await harness.Handler.WaitForInvocations(2);

            Assert.Equal(
                [("p0", 0), ("p2", 2)],
                harness.Handler.Invocations.OrderBy(invocation => invocation.Partition).Select(invocation => (invocation.Messages.Single(), invocation.Partition!.Value)));
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StoresTheSequenceNumberPerPartition()
        {
            var harness = CreatePartitionedHarness(withCheckpoint: true);
            await harness.Start();
            await harness.Processor.Assign("0");
            await harness.Processor.Assign("1");

            await harness.Processor.Deliver("0", TestEvents.Create(7));
            await harness.Processor.Deliver("1", TestEvents.Create(3));
            await WaitUntil(() => harness.Checkpoint.Saves.Count(save => !save.IsBootstrapSeed) == 2);

            Assert.Contains((0, 7L, false), harness.Checkpoint.Saves);
            Assert.Contains((1, 3L, false), harness.Checkpoint.Saves);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StartsEachPartitionAfterItsOwnStoredSequenceNumber()
        {
            var harness = CreatePartitionedHarness(withCheckpoint: true);
            harness.Checkpoint.Store(0, 10);
            harness.Checkpoint.Store(1, 99);
            await harness.Start();

            var partitionZero = await harness.Processor.Assign("0");
            var partitionOne = await harness.Processor.Assign("1");

            Assert.Equal(EventPosition.FromSequenceNumber(10, isInclusive: false), partitionZero);
            Assert.Equal(EventPosition.FromSequenceNumber(99, isInclusive: false), partitionOne);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WithACheckpointAndLast_SeedsEachEmptyPartitionWithMinusOne()
        {
            var harness = CreatePartitionedHarness(StartPosition.Last(), withCheckpoint: true);
            await harness.Start();

            var position = await harness.Processor.Assign("1");

            Assert.Contains((1, -1L, true), harness.Checkpoint.Saves);
            Assert.Equal(EventPosition.FromSequenceNumber(-1, isInclusive: false), position);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenAPartitionIsLost_StopsItsLoopAndLeavesTheOtherPartitionsRunning()
        {
            var harness = CreatePartitionedHarness();
            await harness.Start();
            await harness.Processor.Assign("0");
            await harness.Processor.Assign("1");

            await harness.Processor.Lose("0");
            await harness.Processor.Deliver("0", TestEvents.Create(0, "dropped"));
            await harness.Processor.Deliver("1", TestEvents.Create(0, "kept"));
            await harness.Handler.WaitForInvocations(1);

            Assert.Equal(["kept"], harness.Handler.Invocations.Single().Messages);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenAPartitionIsLostAndAssignedAgain_ResumesAfterTheStoredSequenceNumber()
        {
            var harness = CreatePartitionedHarness(withCheckpoint: true);
            await harness.Start();
            await harness.Processor.Assign("0");
            await harness.Processor.Deliver("0", TestEvents.Create(5));
            await WaitUntil(() => harness.Checkpoint.Saves.Any(save => save is { Partition: 0, IsBootstrapSeed: false }));
            await harness.Processor.Lose("0");

            var position = await harness.Processor.Assign("0");

            Assert.Equal(EventPosition.FromSequenceNumber(5, isInclusive: false), position);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenOnePartitionHalts_OnlyThatPartitionStops()
        {
            var harness = CreatePartitionedHarness(onHandle: context => context.Partition == 0 ? throw new InvalidOperationException("handler failed") : Task.CompletedTask);
            await harness.Start();
            await harness.Processor.Assign("0");
            await harness.Processor.Assign("1");

            await harness.Processor.Deliver("0", TestEvents.Create(0, "poison"));
            var blocked = await DeliverUntilBlocked(harness.Processor, "0");
            await harness.Processor.Deliver("1", TestEvents.Create(0, "healthy"));
            await harness.Handler.WaitForInvocations(2);

            Assert.False(blocked.IsCompleted);
            Assert.Equal(0, harness.Processor.Stops);

            await harness.Processor.Lose("0");
            await blocked.WaitAsync(TestTimeout);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RecordsEachAssignedPartitionAsRunningAndRemovesALostOne()
        {
            var harness = CreatePartitionedHarness();
            await harness.Start();
            await harness.Processor.Assign("0");
            await harness.Processor.Assign("1");

            await harness.Processor.Lose("0");

            Assert.Null(harness.StateRegistry.Get(PartitionKey(0)));
            Assert.Equal(StreamPartitionState.Running, harness.StateRegistry.Get(PartitionKey(1))!.State);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenOnePartitionHalts_RecordsItAsHaltedAndKeepsTheOtherRunning()
        {
            var harness = CreatePartitionedHarness(onHandle: context => context.Partition == 0 ? throw new InvalidOperationException("handler failed") : Task.CompletedTask);
            await harness.Start();
            await harness.Processor.Assign("0");
            await harness.Processor.Assign("1");

            await harness.Processor.Deliver("0", TestEvents.Create(0, "poison"));
            var blocked = await DeliverUntilBlocked(harness.Processor, "0");

            var halted = harness.StateRegistry.Get(PartitionKey(0))!;
            Assert.Equal(StreamPartitionState.Halted, halted.State);
            Assert.Equal("handler failed", halted.LastError!.Message);
            Assert.Equal(StreamPartitionState.Running, harness.StateRegistry.Get(PartitionKey(1))!.State);

            await harness.Processor.Lose("0");
            await blocked.WaitAsync(TestTimeout);

            Assert.Null(harness.StateRegistry.Get(PartitionKey(0)));
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenTheStartPositionCannotBeResolved_RecordsWaitingUntilTheSdkInitializesThePartitionAgain()
        {
            var harness = CreatePartitionedHarness(withCheckpoint: true);
            harness.Checkpoint.GetPositionFailure = new TimeoutException("store down");
            await harness.Start();

            await Assert.ThrowsAsync<TimeoutException>(() => harness.Processor.Assign("0"));

            var waiting = harness.StateRegistry.Get(PartitionKey(0))!;
            Assert.Equal(StreamPartitionState.Waiting, waiting.State);
            Assert.Equal("store down", waiting.LastError!.Message);

            harness.Checkpoint.GetPositionFailure = null;
            await harness.Processor.Assign("0");

            Assert.Equal(StreamPartitionState.Running, harness.StateRegistry.Get(PartitionKey(0))!.State);
            await harness.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task OnStop_RemovesEveryPartition()
        {
            var harness = CreatePartitionedHarness();
            await harness.Start();
            await harness.Processor.Assign("0");
            await harness.Processor.Assign("1");

            await harness.Service.StopAsync(CancellationToken.None);

            Assert.Empty(harness.StateRegistry.GetAll());
        }

        private static StreamPartitionKey PartitionKey(int partition) => new(EventHubName, "ReadingsHandler", partition);

        [Fact]
        public async Task OnStop_StopsTheSdkProcessor()
        {
            var harness = CreatePartitionedHarness();
            await harness.Start();
            await harness.Processor.Assign("0");

            await harness.Service.StopAsync(CancellationToken.None);

            Assert.Equal(1, harness.Processor.Stops);
        }

        [Fact]
        public async Task ReadsTheEventHubThroughTheConsumerGroupNamedByTheConsumerName()
        {
            var harness = CreatePartitionedHarness();
            await harness.Start();

            Assert.Equal("ReadingsHandler", harness.Processor.ConsumerGroup);
            await harness.Service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class Harness
    {
        public required StreamPartitionStateRegistry StateRegistry { get; init; }

        public required FakeEventHubsProcessorFactory ProcessorFactory { get; init; }

        public required FakeEventHubsInspector Inspector { get; init; }

        public required FakeMessageStreamCheckpoint<string> Checkpoint { get; init; }

        public required SpyStreamHandler Handler { get; init; }

        public required MessageStreamProcessingBackgroundService<string, SpyStreamHandler> Service { get; init; }

        public FakeEventHubsProcessor Processor => ProcessorFactory.Processor;

        public async Task Start()
        {
            await Service.StartAsync(CancellationToken.None);
            await WaitUntil(() => ProcessorFactory.Created.Count == 1 && ProcessorFactory.Created[0].Starts == 1);
        }
    }

    private sealed class PartitionedHarness
    {
        public required StreamPartitionStateRegistry StateRegistry { get; init; }

        public required FakeEventHubsProcessorFactory ProcessorFactory { get; init; }

        public required FakeMessageStreamCheckpoint<string> Checkpoint { get; init; }

        public required SpyStreamHandler Handler { get; init; }

        public required PartitionedMessageStreamProcessingBackgroundService<string, SpyStreamHandler> Service { get; init; }

        public FakeEventHubsProcessor Processor => ProcessorFactory.Processor;

        public async Task Start()
        {
            await Service.StartAsync(CancellationToken.None);
            await WaitUntil(() => ProcessorFactory.Created.Count == 1 && ProcessorFactory.Created[0].Starts == 1);
        }
    }

    private sealed class SpyStreamHandler(Func<IMessageStreamContext<string>, Task>? onHandle) : MessageStreamHandler<string>
    {
        private readonly Lock _lock = new();
        private readonly List<TaskCompletionSource> _waiters = [];

        public List<(IReadOnlyCollection<string> Messages, int? Partition)> Invocations { get; } = [];

        public override async Task Handle(IMessageStreamContext<string> messageContext)
        {
            List<TaskCompletionSource> toSignal;
            lock (_lock)
            {
                Invocations.Add((messageContext.Messages, messageContext.Partition));
                toSignal = [.. _waiters];
            }

            foreach (var waiter in toSignal)
            {
                waiter.TrySetResult();
            }

            if (onHandle is not null)
                await onHandle(messageContext);
        }

        public async Task WaitForInvocations(int count)
        {
            while (true)
            {
                TaskCompletionSource waiter;
                lock (_lock)
                {
                    if (Invocations.Count >= count)
                        return;

                    waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _waiters.Add(waiter);
                }

                await waiter.Task.WaitAsync(TestTimeout);
            }
        }
    }
}
