using System.Threading.Channels;
using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Flowly.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class PartitionedMessageStreamProcessingBackgroundServiceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private static (PartitionedMessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler> BackgroundService, FakePartitionedStreamConsumer Consumer, SpyStreamHandler Handler, SpyHandlerInstrumentation Instrumentation, FakeCheckpoint? Checkpoint)
        CreateService(
            int maxMessages,
            int maxRetries,
            Func<IMessageStreamContext<TestMessage>, Task>? onHandle = null,
            FakeCheckpoint? checkpoint = null,
            Func<FakePartitionedStreamConsumer, ITransportClient>? clientFactory = null,
            StartPosition? startPosition = null,
            IStreamStartOffsetResolver? startOffsetResolver = null,
            string? consumerName = null,
            StreamPartitionStateRegistry? stateRegistry = null,
            bool retryingConsumer = false,
            StreamPartitionResumeRegistry? resumeRegistry = null)
    {
        var consumer = retryingConsumer ? new RetryingFakePartitionedStreamConsumer() : new FakePartitionedStreamConsumer();
        ITransportClient client = clientFactory?.Invoke(consumer)
            ?? (startOffsetResolver is null ? new FakePartitionedStreamClient(consumer) : new ResolverCapablePartitionedStreamClient(consumer, startOffsetResolver));
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
                consumerName ?? nameof(SpyStreamHandler),
                3,
                startPosition ?? StartPosition.First(),
                maxMessages,
                TimeSpan.FromSeconds(30),
                maxRetries,
                0),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PartitionedMessageStreamProcessingBackgroundService<TestMessage, SpyStreamHandler>>.Instance,
            instrumentation,
                stateRegistry ?? new StreamPartitionStateRegistry(TimeProvider.System),
                resumeRegistry ?? new StreamPartitionResumeRegistry());

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
                new SpyHandlerInstrumentation(),
                new StreamPartitionStateRegistry(TimeProvider.System),
                new StreamPartitionResumeRegistry());

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
        public async Task PassesTheDefaultedConsumerNameInTheConsumerOptions()
        {
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            Assert.Equal(nameof(SpyStreamHandler), consumer.Client!.ReceivedOptions!.ConsumerName);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task PassesTheExplicitConsumerNameInTheConsumerOptions()
        {
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, consumerName: "reporting-group");

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            Assert.Equal("reporting-group", consumer.Client!.ReceivedOptions!.ConsumerName);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task PassesTheSameConsumerNameToTheConsumerAndTheCheckpoint()
        {
            var checkpoint = new FakeCheckpoint();
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, consumerName: "reporting-group");

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            await consumer.Client!.ReceivedResolveStartPosition!(1, CancellationToken.None);

            Assert.Equal("reporting-group", checkpoint.InitializeCalls.Single().ConsumerName);
            Assert.Equal(checkpoint.InitializeCalls.Single().ConsumerName, consumer.Client.ReceivedOptions!.ConsumerName);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task OnStreamOnlyClient_ProcessesAnAssignedPartition()
        {
            var (backgroundService, consumer, handler, _, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                clientFactory: streamConsumer => new StreamOnlyPartitionedClient(streamConsumer));

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var processor = await consumer.AssignPartition(0);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("a")));

            await handler.WaitForInvocations(1, TestTimeout);
            Assert.Equal(0, handler.Invocations.Single().Partition);

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
            await WaitUntil(() => instrumentation.SucceededCounts.Count > 0, TestTimeout);

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
            await instrumentation.NextHalt();
            Assert.Contains("permanent failure", instrumentation.HaltedReasons);

            await processorOne.Deliver(new FakeReceivedMessage(new TestMessage("still-works")));
            await handler.WaitForInvocations(2, TestTimeout);

            Assert.Contains(handler.Invocations, i => i.Partition == 1 && i.Messages.Single().Payload == "still-works");

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenPartitionHalts_KeepsItsProcessorUntilItIsRevoked()
        {
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: _ => throw new InvalidOperationException("permanent failure"));

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var processor = await consumer.AssignPartition(0);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("poison")));
            await instrumentation.NextHalt();

            Assert.Equal(0, processor.DisposeAsyncCallCount);

            await consumer.RevokePartition(0);

            Assert.Equal(1, processor.DisposeAsyncCallCount);

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
        public async Task WhenInitializeCheckpointFailsOnFirstAssignment_InitializesAgainOnNextAssignmentOfThatPartition()
        {
            var attempts = 0;
            var checkpoint = new FakeCheckpoint
            {
                StoredPosition = 9,
                OnInitialize = _ => ++attempts == 1 ? throw new InvalidOperationException("store unavailable") : Task.CompletedTask
            };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None));
            var startPosition = await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);

            Assert.Equal(StartPosition.Offset(10), startPosition);
            Assert.Equal(2, checkpoint.InitializeCalls.Count(c => c.Partition == 2));

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenInitializeCheckpointSucceededAfterAFailure_DoesNotInitializeThatPartitionAgain()
        {
            var attempts = 0;
            var checkpoint = new FakeCheckpoint
            {
                StoredPosition = 9,
                OnInitialize = _ => ++attempts == 1 ? throw new InvalidOperationException("store unavailable") : Task.CompletedTask
            };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None));
            await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);
            await consumer.Client.ReceivedResolveStartPosition!(2, CancellationToken.None);

            Assert.Equal(2, checkpoint.InitializeCalls.Count(c => c.Partition == 2));

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenInitializeCheckpointSucceeds_DoesNotInitializeThatPartitionAgain()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 9 };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);
            await consumer.Client.ReceivedResolveStartPosition!(2, CancellationToken.None);
            await consumer.Client.ReceivedResolveStartPosition!(2, CancellationToken.None);

            Assert.Single(checkpoint.InitializeCalls, c => c.Partition == 2);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenInitializeCheckpointFailsForOnePartition_StillInitializesOtherPartitionsOnce()
        {
            var checkpoint = new FakeCheckpoint
            {
                StoredPosition = 9,
                OnInitialize = context => context.Partition == 0 ? throw new InvalidOperationException("store unavailable") : Task.CompletedTask
            };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.Client!.ReceivedResolveStartPosition!(0, CancellationToken.None));
            await consumer.Client!.ReceivedResolveStartPosition!(1, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.Client.ReceivedResolveStartPosition!(0, CancellationToken.None));
            await consumer.Client.ReceivedResolveStartPosition!(1, CancellationToken.None);

            Assert.Equal(2, checkpoint.InitializeCalls.Count(c => c.Partition == 0));
            Assert.Single(checkpoint.InitializeCalls, c => c.Partition == 1);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenDifferentPartitionsAreFirstAssignedConcurrently_InitializesEachPartitionOnce()
        {
            var entered = 0;
            var allEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var checkpoint = new FakeCheckpoint
            {
                StoredPosition = 9,
                OnInitialize = async _ =>
                {
                    if (Interlocked.Increment(ref entered) == 3)
                        allEntered.SetResult();

                    await allEntered.Task;
                }
            };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await Task.WhenAll(Enumerable.Range(0, 3).Select(partition => consumer.Client!.ReceivedResolveStartPosition!(partition, CancellationToken.None))).WaitAsync(TestTimeout);
            await Task.WhenAll(Enumerable.Range(0, 3).Select(partition => consumer.Client!.ReceivedResolveStartPosition!(partition, CancellationToken.None))).WaitAsync(TestTimeout);

            Assert.Equal([0, 1, 2], checkpoint.InitializeCalls.Select(c => c.Partition!.Value).Order());

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
        public async Task WhenClientIsResolverCapableAndPartitionHasNoPosition_SeedsThatPartitionAndStartsFromResolvedOffset()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 30 };
            var (backgroundService, consumer, _, _, _) = CreateService(
                maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, startPosition: StartPosition.Last(), startOffsetResolver: resolver);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var startPosition = await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);

            Assert.Equal(StartPosition.Offset(30), startPosition);
            var save = Assert.Single(checkpoint.SaveCalls);
            Assert.Equal(2, save.Partition);
            Assert.Equal(29, save.Position);
            Assert.True(save.IsBootstrapSeed);
            var call = Assert.Single(resolver.Calls);
            Assert.Equal("telemetry-reading", call.QueueName);
            Assert.Equal(2, call.Partition);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenPartitionIsReassignedWithoutAStoredPosition_SeedsWithoutReinitializingTheCheckpoint()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 30 };
            var (backgroundService, consumer, _, _, _) = CreateService(
                maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, startPosition: StartPosition.Last(), startOffsetResolver: resolver);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);
            checkpoint.StoredPosition = null;
            await consumer.Client.ReceivedResolveStartPosition!(2, CancellationToken.None);

            Assert.Single(checkpoint.InitializeCalls, c => c.Partition == 2);
            Assert.Equal(2, checkpoint.SaveCalls.Count);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenClientIsNotResolverCapable_UsesConfiguredStartPositionWithoutSeeding()
        {
            var checkpoint = new FakeCheckpoint();
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, startPosition: StartPosition.Last());

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var startPosition = await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);

            Assert.Equal(StartPosition.Last(), startPosition);
            Assert.Empty(checkpoint.SaveCalls);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenClientIsResolverCapableButNoCheckpointIsRegistered_DoesNotResolve()
        {
            var resolver = new FakeStreamStartOffsetResolver { Result = 30 };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, startPosition: StartPosition.Last(), startOffsetResolver: resolver);

            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            var startPosition = await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);

            Assert.Empty(resolver.Calls);
            Assert.Equal(StartPosition.Last(), startPosition);

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

    public class StreamPartitionStates
    {
        private static StreamPartitionKey Key(int partition) => new("telemetry-reading", nameof(SpyStreamHandler), partition);

        [Fact]
        public async Task OnPartitionAssigned_RecordsRunningForThatPartitionOnly()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await consumer.AssignPartition(1);

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(1))!.State);
            Assert.Null(registry.Get(Key(0)));

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task OnPartitionRevoked_RemovesThatPartitionsEntryButNotOthers()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            await consumer.AssignPartition(0);
            await consumer.AssignPartition(1);

            await consumer.RevokePartition(0);

            Assert.Null(registry.Get(Key(0)));
            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(1))!.State);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenAPartitionHalts_RecordsHaltedWithTheLastError()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: ctx => ctx.Partition == 0 ? throw new InvalidOperationException("permanent failure") : Task.CompletedTask,
                stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var processorZero = await consumer.AssignPartition(0);
            await consumer.AssignPartition(1);

            await processorZero.Deliver(new FakeReceivedMessage(new TestMessage("poison")));
            await instrumentation.NextHalt();

            var halted = registry.Get(Key(0))!;
            Assert.Equal(StreamPartitionState.Halted, halted.State);
            Assert.Equal("permanent failure", halted.LastError!.Message);
            Assert.Equal(typeof(InvalidOperationException).FullName, halted.LastError.Type);
            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(1))!.State);
            Assert.Contains((0, StreamPartitionState.Halted), instrumentation.Transitions);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenAHaltedPartitionIsRevoked_RemovesItsEntry()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: _ => throw new InvalidOperationException("permanent failure"),
                stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var processor = await consumer.AssignPartition(0);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("poison")));
            await instrumentation.NextHalt();

            await consumer.RevokePartition(0);

            Assert.Null(registry.Get(Key(0)));

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenAHaltedPartitionIsAssignedAgain_RecordsRunning()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: _ => throw new InvalidOperationException("permanent failure"),
                stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var processor = await consumer.AssignPartition(0);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("poison")));
            await instrumentation.NextHalt();

            await consumer.RevokePartition(0);
            await consumer.AssignPartition(0);

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(0))!.State);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenStartPositionResolutionFails_RecordsWaitingWithTheError()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var checkpoint = new FakeCheckpoint { StoredPosition = 9, OnInitialize = _ => throw new TimeoutException("store down") };
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await Assert.ThrowsAsync<TimeoutException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None));

            var waiting = registry.Get(Key(2))!;
            Assert.Equal(StreamPartitionState.Waiting, waiting.State);
            Assert.Equal("store down", waiting.LastError!.Message);
            Assert.Equal([(2, StreamPartitionState.Waiting)], instrumentation.Transitions);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenStartPositionResolutionLaterSucceedsAndThePartitionIsAssigned_RecordsRunningAndKeepsTheLastError()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var attempts = 0;
            var checkpoint = new FakeCheckpoint
            {
                StoredPosition = 9,
                OnInitialize = _ => ++attempts == 1 ? throw new TimeoutException("store down") : Task.CompletedTask
            };
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            await Assert.ThrowsAsync<TimeoutException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None));

            await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);
            Assert.Equal(StreamPartitionState.Waiting, registry.Get(Key(2))!.State);

            await consumer.AssignPartition(2);

            var running = registry.Get(Key(2))!;
            Assert.Equal(StreamPartitionState.Running, running.State);
            Assert.Equal("store down", running.LastError!.Message);
            Assert.Equal([(2, StreamPartitionState.Waiting), (2, StreamPartitionState.Running)], instrumentation.Transitions);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenStartPositionResolutionFailsRepeatedly_KeepsWaitingAndCountsOneTransition()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var attempts = 0;
            var checkpoint = new FakeCheckpoint { StoredPosition = 9, OnInitialize = _ => throw new TimeoutException($"attempt {++attempts}") };
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await Assert.ThrowsAsync<TimeoutException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None));
            await Assert.ThrowsAsync<TimeoutException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None));

            Assert.Equal("attempt 2", registry.Get(Key(2))!.LastError!.Message);
            Assert.Single(instrumentation.Transitions);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenTheConsumerRetriesStartPositionsItself_ADeclinedAttemptDoesNotRecordWaiting()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var checkpoint = new FakeCheckpoint { StoredPosition = 9, OnInitialize = _ => throw new TimeoutException("store down") };
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(
                maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, stateRegistry: registry, retryingConsumer: true);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await Assert.ThrowsAsync<TimeoutException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None));

            Assert.Null(registry.Get(Key(2)));
            Assert.Empty(instrumentation.Transitions);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenTheConsumerRetriesStartPositionsItselfAndALaterAttemptSucceeds_GoesStraightToRunning()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var attempts = 0;
            var checkpoint = new FakeCheckpoint
            {
                StoredPosition = 9,
                OnInitialize = _ => ++attempts == 1 ? throw new TimeoutException("store down") : Task.CompletedTask
            };
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(
                maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, stateRegistry: registry, retryingConsumer: true);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            await Assert.ThrowsAsync<TimeoutException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None));

            await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);
            await consumer.AssignPartition(2);

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(2))!.State);
            Assert.Null(registry.Get(Key(2))!.LastError);
            Assert.Equal([(2, StreamPartitionState.Running)], instrumentation.Transitions);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenTheConsumerRetriesStartPositionsItself_RecordsWaitingWhenItReportsTheRetryWindowEnded()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(maxMessages: 1, maxRetries: 0, stateRegistry: registry, retryingConsumer: true);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            ((RetryingFakePartitionedStreamConsumer)consumer).RaiseExhausted(2, new TimeoutException("store down"));

            var waiting = registry.Get(Key(2))!;
            Assert.Equal(StreamPartitionState.Waiting, waiting.State);
            Assert.Equal("store down", waiting.LastError!.Message);
            Assert.Equal([(2, StreamPartitionState.Waiting)], instrumentation.Transitions);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenTheConsumerRetriesStartPositionsItselfAndTheRetryWindowEndsAgain_KeepsWaitingAndCountsOneTransition()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(maxMessages: 1, maxRetries: 0, stateRegistry: registry, retryingConsumer: true);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var retryingConsumer = (RetryingFakePartitionedStreamConsumer)consumer;

            retryingConsumer.RaiseExhausted(2, new TimeoutException("first"));
            retryingConsumer.RaiseExhausted(2, new TimeoutException("second"));

            Assert.Equal("second", registry.Get(Key(2))!.LastError!.Message);
            Assert.Single(instrumentation.Transitions);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenTheConsumerRetriesStartPositionsItselfAndTheRetryWindowEndsForARunningPartition_DoesNotRecordWaiting()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, stateRegistry: registry, retryingConsumer: true);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            await consumer.AssignPartition(2);

            ((RetryingFakePartitionedStreamConsumer)consumer).RaiseExhausted(2, new TimeoutException("store down"));

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(2))!.State);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenAWaitingPartitionIsRevoked_RemovesItsEntry()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var checkpoint = new FakeCheckpoint { StoredPosition = 9, OnInitialize = _ => throw new TimeoutException("store down") };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            await Assert.ThrowsAsync<TimeoutException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None));

            await consumer.RevokePartition(2);

            Assert.Null(registry.Get(Key(2)));

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenStartPositionResolutionFailsWhileTheLoopStillRuns_StaysRunning()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var checkpoint = new FakeCheckpoint { StoredPosition = 9, OnInitialize = _ => throw new TimeoutException("store down") };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            await consumer.AssignPartition(2);

            await Assert.ThrowsAsync<TimeoutException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None));

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(2))!.State);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenStartPositionResolutionIsCancelledByShutdown_DoesNotRecordWaiting()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            using var cancellation = new CancellationTokenSource();
            var checkpoint = new FakeCheckpoint { StoredPosition = 9, OnInitialize = _ => throw new OperationCanceledException(cancellation.Token) };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            await cancellation.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => consumer.Client!.ReceivedResolveStartPosition!(2, cancellation.Token));

            Assert.Null(registry.Get(Key(2)));

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenNoCheckpointIsRegistered_ResolvingTheStartPositionRecordsNothing()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);

            await consumer.Client!.ReceivedResolveStartPosition!(2, CancellationToken.None);

            Assert.Empty(registry.GetAll());

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task OnStop_RemovesEveryEntry()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var checkpoint = new FakeCheckpoint { StoredPosition = 9, OnInitialize = _ => throw new TimeoutException("store down") };
            var (backgroundService, consumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, checkpoint: checkpoint, stateRegistry: registry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            await consumer.AssignPartition(0);
            await Assert.ThrowsAsync<TimeoutException>(() => consumer.Client!.ReceivedResolveStartPosition!(1, CancellationToken.None));

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.Empty(registry.GetAll());
        }

        [Fact]
        public async Task StatesOfDifferentConsumersOfTheSameQueueAreKeptApart()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (first, firstConsumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, consumerName: "first", stateRegistry: registry);
            var (second, secondConsumer, _, _, _) = CreateService(maxMessages: 1, maxRetries: 0, consumerName: "second", stateRegistry: registry);
            await first.StartAsync(CancellationToken.None);
            await second.StartAsync(CancellationToken.None);
            await firstConsumer.Started.Task.WaitAsync(TestTimeout);
            await secondConsumer.Started.Task.WaitAsync(TestTimeout);
            await firstConsumer.AssignPartition(0);
            await secondConsumer.AssignPartition(0);

            await first.StopAsync(CancellationToken.None);

            var remaining = Assert.Single(registry.GetAll());
            Assert.Equal("second", remaining.Key.ConsumerName);

            await second.StopAsync(CancellationToken.None);
        }
    }

    public class HaltedPartitions
    {
        private static StreamPartitionKey Key(int partition) => new("telemetry-reading", nameof(SpyStreamHandler), partition);

        [Fact]
        public async Task WhenResumed_RunsTheHeldBatchAgainAndContinuesWithTheMessagesBehindIt()
        {
            var failures = 1;
            var resumeRegistry = new StreamPartitionResumeRegistry();
            var (backgroundService, consumer, handler, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: _ => Interlocked.Decrement(ref failures) >= 0 ? throw new InvalidOperationException("transient") : Task.CompletedTask,
                resumeRegistry: resumeRegistry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var processor = await consumer.AssignPartition(0);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("a")));
            await instrumentation.NextHalt();
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("b")));
            var blocked = processor.Deliver(new FakeReceivedMessage(new TestMessage("c")));

            var result = resumeRegistry.Resume(Key(0));
            await blocked.WaitAsync(TestTimeout);
            await handler.WaitForInvocations(4, TestTimeout);

            Assert.Equal(StreamPartitionResumeResult.Resumed, result);
            Assert.Equal(["a", "a", "b", "c"], handler.Invocations.SelectMany(invocation => invocation.Messages).Select(message => message.Payload));

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhileTheBatchIsHeld_DoesNotCheckpointAndAfterResumeCheckpointsTheHeldBatch()
        {
            var failures = 1;
            var resumeRegistry = new StreamPartitionResumeRegistry();
            var checkpoint = new FakeCheckpoint();
            var (backgroundService, consumer, handler, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: _ => Interlocked.Decrement(ref failures) >= 0 ? throw new InvalidOperationException("transient") : Task.CompletedTask,
                checkpoint: checkpoint,
                resumeRegistry: resumeRegistry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var processor = await consumer.AssignPartition(2);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("a"), streamOffset: 11));
            await instrumentation.NextHalt();

            Assert.Empty(checkpoint.SaveCalls);

            resumeRegistry.Resume(Key(2));
            await checkpoint.FirstSaved.Task.WaitAsync(TestTimeout);

            var save = Assert.Single(checkpoint.SaveCalls);
            Assert.Equal(2, save.Partition);
            Assert.Equal(11, save.Position);
            Assert.Equal(2, handler.Invocations.Count);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenRevokedWhileHeld_ReleasesTheBlockedDeliveryAndRemovesTheHandle()
        {
            var resumeRegistry = new StreamPartitionResumeRegistry();
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: _ => throw new InvalidOperationException("permanent failure"),
                stateRegistry: registry,
                resumeRegistry: resumeRegistry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var processor = await consumer.AssignPartition(0);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("a")));
            await instrumentation.NextHalt();
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("b")));
            var blocked = processor.Deliver(new FakeReceivedMessage(new TestMessage("c")));

            await consumer.RevokePartition(0);
            await blocked.WaitAsync(TestTimeout);

            Assert.Equal(StreamPartitionResumeResult.NotOwned, resumeRegistry.Resume(Key(0)));
            Assert.Null(registry.Get(Key(0)));
            Assert.Equal(1, processor.DisposeAsyncCallCount);

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenAHeldPartitionIsRevokedAndAssignedAgainAsInAConsumerRecreation_ReplaysTheBatchAndHaltsAgain()
        {
            var resumeRegistry = new StreamPartitionResumeRegistry();
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, handler, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: context => context.Messages.Any(message => message.Payload == "poison") ? throw new InvalidOperationException("permanent failure") : Task.CompletedTask,
                stateRegistry: registry,
                resumeRegistry: resumeRegistry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var firstProcessor = await consumer.AssignPartition(0);
            await firstProcessor.Deliver(new FakeReceivedMessage(new TestMessage("poison")));
            await instrumentation.NextHalt();

            await consumer.RevokePartition(0);

            Assert.Null(registry.Get(Key(0)));
            Assert.Equal(StreamPartitionResumeResult.NotOwned, resumeRegistry.Resume(Key(0)));

            var secondProcessor = await consumer.AssignPartition(0);

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(0))!.State);

            await secondProcessor.Deliver(new FakeReceivedMessage(new TestMessage("poison")));
            await instrumentation.NextHalt();

            Assert.Equal(StreamPartitionState.Halted, registry.Get(Key(0))!.State);
            Assert.Equal(2, handler.Invocations.Count);
            Assert.Equal(1, firstProcessor.DisposeAsyncCallCount);
            Assert.Equal(StreamPartitionResumeResult.Resumed, resumeRegistry.Resume(Key(0)));

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenAssignedAgainAfterBeingHeld_RunsNormallyWithoutAHeldBatch()
        {
            var resumeRegistry = new StreamPartitionResumeRegistry();
            var (backgroundService, consumer, handler, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: context => context.Messages.Any(message => message.Payload == "poison") ? throw new InvalidOperationException("permanent failure") : Task.CompletedTask,
                resumeRegistry: resumeRegistry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var first = await consumer.AssignPartition(0);
            await first.Deliver(new FakeReceivedMessage(new TestMessage("poison")));
            await instrumentation.NextHalt();
            await consumer.RevokePartition(0);

            var second = await consumer.AssignPartition(0);
            await second.Deliver(new FakeReceivedMessage(new TestMessage("after")));
            await handler.WaitForInvocations(2, TestTimeout);

            Assert.Equal(StreamPartitionResumeResult.NotHalted, resumeRegistry.Resume(Key(0)));

            await backgroundService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WhenStoppedWhileHeld_CompletesAndReleasesTheBlockedDelivery()
        {
            var resumeRegistry = new StreamPartitionResumeRegistry();
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: _ => throw new InvalidOperationException("permanent failure"),
                stateRegistry: registry,
                resumeRegistry: resumeRegistry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var processor = await consumer.AssignPartition(1);
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("a")));
            await instrumentation.NextHalt();
            await processor.Deliver(new FakeReceivedMessage(new TestMessage("b")));
            var blocked = processor.Deliver(new FakeReceivedMessage(new TestMessage("c")));

            await backgroundService.StopAsync(CancellationToken.None).WaitAsync(TestTimeout);
            await blocked.WaitAsync(TestTimeout);

            Assert.Equal(StreamPartitionResumeResult.NotOwned, resumeRegistry.Resume(Key(1)));
            Assert.Empty(registry.GetAll());
            Assert.Equal(1, processor.DisposeAsyncCallCount);
        }

        [Fact]
        public async Task WhenOnePartitionIsResumed_AnotherHeldPartitionStaysHalted()
        {
            var failures = 1;
            var resumeRegistry = new StreamPartitionResumeRegistry();
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            var (backgroundService, consumer, _, instrumentation, _) = CreateService(
                maxMessages: 1,
                maxRetries: 0,
                onHandle: context => context.Partition == 1 || Interlocked.Decrement(ref failures) >= 0 ? throw new InvalidOperationException("failure") : Task.CompletedTask,
                stateRegistry: registry,
                resumeRegistry: resumeRegistry);
            await backgroundService.StartAsync(CancellationToken.None);
            await consumer.Started.Task.WaitAsync(TestTimeout);
            var zero = await consumer.AssignPartition(0);
            var one = await consumer.AssignPartition(1);
            await zero.Deliver(new FakeReceivedMessage(new TestMessage("a")));
            await instrumentation.NextHalt();
            await one.Deliver(new FakeReceivedMessage(new TestMessage("b")));
            await instrumentation.NextHalt();

            resumeRegistry.Resume(Key(0));

            Assert.Equal(StreamPartitionState.Halted, registry.Get(Key(1))!.State);

            await backgroundService.StopAsync(CancellationToken.None);
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

    private class FakePartitionedStreamClient(FakePartitionedStreamConsumer consumer) : IMessageBusClient, IPartitionedStreamCapableMessageBusClient
    {
        public string? ReceivedQueueName { get; private set; }
        public int ReceivedPartitionCount { get; private set; }
        public MessageBusProcessorOptions? ReceivedOptions { get; private set; }
        public Func<int, CancellationToken, Task<StartPosition>>? ReceivedResolveStartPosition { get; private set; }

        public string MessagingSystem => "fake-partitioned-stream";

        public Task<IPartitionedStreamConsumer<TMessage>> CreatePartitionedStreamConsumer<TMessage>(
            string queueName, int partitionCount, Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition, MessageBusProcessorOptions options,
            ILogger logger)
        {
            ReceivedQueueName = queueName;
            ReceivedPartitionCount = partitionCount;
            ReceivedOptions = options;
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

    private sealed class ResolverCapablePartitionedStreamClient(FakePartitionedStreamConsumer consumer, IStreamStartOffsetResolver startOffsetResolver)
        : FakePartitionedStreamClient(consumer), IStreamStartOffsetResolver
    {
        public Task<long?> ResolveStartOffset(string queueName, int? partition, StartPosition startPosition, CancellationToken cancellationToken)
            => startOffsetResolver.ResolveStartOffset(queueName, partition, startPosition, cancellationToken);
    }

    private sealed class StreamOnlyPartitionedClient(FakePartitionedStreamConsumer consumer) : IStreamCapableMessageBusClient, IPartitionedStreamCapableMessageBusClient
    {
        public string MessagingSystem => "fake-stream-only-partitioned";

        public Task<IPartitionedStreamConsumer<TMessage>> CreatePartitionedStreamConsumer<TMessage>(
            string queueName, int partitionCount, Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition, MessageBusProcessorOptions options,
            ILogger logger)
            => Task.FromResult((IPartitionedStreamConsumer<TMessage>)(object)consumer);

        public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
            => throw new NotSupportedException();
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

    private sealed class RetryingFakePartitionedStreamConsumer : FakePartitionedStreamConsumer, IStartPositionRetryingConsumer
    {
        public event Action<int, Exception>? StartPositionResolutionExhausted;

        public void RaiseExhausted(int partition, Exception exception) => StartPositionResolutionExhausted?.Invoke(partition, exception);
    }

    private class FakePartitionedStreamConsumer : IPartitionedStreamConsumer<TestMessage>
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
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            Disposed.TrySetResult();
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
        private readonly Lock _initializeLock = new();

        public long? StoredPosition { get; set; }
        public TaskCompletionSource FirstSaved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<MessageStreamCheckpointContext, Task>? OnInitialize { get; set; }
        public List<MessageStreamCheckpointContext> InitializeCalls { get; } = [];
        public List<MessageStreamCheckpointSaveContext> SaveCalls { get; } = [];

        protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
        {
            lock (_initializeLock)
            {
                InitializeCalls.Add(context);
            }

            return OnInitialize?.Invoke(context) ?? Task.CompletedTask;
        }

        protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
            => Task.FromResult(StoredPosition);

        protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
        {
            SaveCalls.Add(context);
            StoredPosition = context.Position;
            FirstSaved.TrySetResult();
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
        public List<(int? Partition, StreamPartitionState State)> Transitions { get; } = [];
        public Channel<string> Halts { get; } = Channel.CreateUnbounded<string>();

        public async Task NextHalt() => await Halts.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        public bool IsEnabled => false;

        public System.Diagnostics.Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, System.Diagnostics.ActivityContext parentContext = default)
            => null;

        public System.Diagnostics.Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<System.Diagnostics.ActivityLink> links)
            => null;

        public void RecordReceived(string handlerName, string queueName, long count = 1) { }

        public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) => SucceededCounts.Add(count);

        public void RecordFailed(string handlerName, string queueName, long count = 1) { }

        public void RecordRetried(string handlerName, string queueName, long count = 1) => RetriedCounts.Add(count);

        public void RecordHalted(string handlerName, string queueName, string reason)
        {
            HaltedReasons.Add(reason);
            Halts.Writer.TryWrite(reason);
        }

        public void RecordStreamPartitionTransition(string handlerName, string queueName, string consumerName, int? partition, StreamPartitionState state)
            => Transitions.Add((partition, state));

        public void RecordCheckpointSaved(string handlerName, string queueName, double durationMs) { }

        public void RecordCheckpointSaveFailed(string handlerName, string queueName) { }

        public void RecordCheckpointResolved(string handlerName, string queueName, CheckpointResolution resolution) { }

        public System.Diagnostics.Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId) => null;

        public void RecordResponseSent(string callQueueName, double durationMs) { }

        public void RecordResponseFailed(string callQueueName) { }
    }
}
