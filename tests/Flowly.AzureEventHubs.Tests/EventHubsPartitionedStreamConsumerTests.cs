using Azure.Messaging.EventHubs.Consumer;
using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsPartitionedStreamConsumerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private sealed record Fixture(
        EventHubsPartitionedStreamConsumer<string> Consumer,
        FakeEventHubsProcessorFactory ProcessorFactory,
        RecordingLogger<EventHubsPartitionedStreamConsumer<string>> Logger,
        List<int> Assigned,
        Dictionary<int, IMessageBusProcessor<string>> Processors,
        List<int> Revoked,
        List<int> ResolvedPartitions);

    private static async Task<Fixture> Start(
        Func<int, CancellationToken, Task<StartPosition>>? resolveStartPosition = null,
        bool sharesPartitionOwnership = false,
        int partitionCount = 4,
        TimeProvider? timeProvider = null)
    {
        var processorFactory = new FakeEventHubsProcessorFactory();
        var logger = new RecordingLogger<EventHubsPartitionedStreamConsumer<string>>();
        var resolvedPartitions = new List<int>();
        var consumer = new EventHubsPartitionedStreamConsumer<string>(
            processorFactory,
            "readings",
            "group",
            partitionCount,
            (partition, cancellationToken) =>
            {
                resolvedPartitions.Add(partition);

                return resolveStartPosition is null ? Task.FromResult(StartPosition.First()) : resolveStartPosition(partition, cancellationToken);
            },
            sharesPartitionOwnership,
            logger,
            timeProvider ?? TimeProvider.System);
        var assigned = new List<int>();
        var processors = new Dictionary<int, IMessageBusProcessor<string>>();
        var revoked = new List<int>();
        consumer.PartitionAssigned += (partition, processor) =>
        {
            assigned.Add(partition);
            processors[partition] = processor;

            return Task.CompletedTask;
        };
        consumer.PartitionRevoked += partition =>
        {
            revoked.Add(partition);

            return Task.CompletedTask;
        };

        await consumer.StartProcessingMessages();

        return new Fixture(consumer, processorFactory, logger, assigned, processors, revoked, resolvedPartitions);
    }

    public class StartProcessingMessages
    {
        [Fact]
        public async Task CreatesAndStartsTheSdkProcessorForTheEventHubAndConsumerGroup()
        {
            var fixture = await Start();

            Assert.Equal("readings", fixture.ProcessorFactory.Processor.EventHubName);
            Assert.Equal("group", fixture.ProcessorFactory.Processor.ConsumerGroup);
            Assert.Equal(1, fixture.ProcessorFactory.Processor.Starts);
        }

        [Fact]
        public async Task WhenTheSdkProcessorFailsToStart_PropagatesTheFailure()
        {
            var processorFactory = new FakeEventHubsProcessorFactory();
            var consumer = new EventHubsPartitionedStreamConsumer<string>(
                processorFactory,
                "readings",
                "group",
                4,
                (_, _) => Task.FromResult(StartPosition.First()),
                false,
                new RecordingLogger<EventHubsPartitionedStreamConsumer<string>>(),
                TimeProvider.System);
            var failure = new InvalidOperationException("missing consumer group");
            processorFactory.StartFailure = failure;

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.StartProcessingMessages());

            Assert.Same(failure, thrown);
        }
    }

    public class GetStartingPosition
    {
        [Theory]
        [MemberData(nameof(StartPositionsWithTheirEventPositions))]
        public async Task MapsTheResolvedStartPositionToAnEventPosition(StartPosition startPosition, EventPosition expected)
        {
            var fixture = await Start((_, _) => Task.FromResult(startPosition));

            Assert.Equal(expected, await fixture.ProcessorFactory.Processor.Assign("1"));
        }

        public static TheoryData<StartPosition, EventPosition> StartPositionsWithTheirEventPositions()
            => new()
            {
                { StartPosition.First(), EventPosition.Earliest },
                { StartPosition.Last(), EventPosition.Latest },
                { StartPosition.Offset(10), EventPosition.FromSequenceNumber(9, isInclusive: false) },
                { StartPosition.Offset(0), EventPosition.FromSequenceNumber(-1, isInclusive: false) },
                { StartPosition.Timestamp(new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc)), EventPosition.FromEnqueuedTime(new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero)) }
            };

        [Fact]
        public async Task ResolvesOnlyTheAssignedPartition()
        {
            var fixture = await Start();

            await fixture.ProcessorFactory.Processor.Assign("2");

            Assert.Equal([2], fixture.ResolvedPartitions);
        }

        [Fact]
        public async Task ResolvesTheStartPositionOnceWhenTheSdkAsksForItBeforeInitializing()
        {
            var fixture = await Start();

            await fixture.ProcessorFactory.Processor.Assign("1");

            Assert.Equal([1], fixture.ResolvedPartitions);
            Assert.Equal([1], fixture.Assigned);
        }

        [Fact]
        public async Task ResolvesTheStartPositionOnceWhenTheSdkInitializesBeforeAskingForIt()
        {
            var fixture = await Start();

            await fixture.ProcessorFactory.Processor.AssignInitializingFirst("1");

            Assert.Equal([1], fixture.ResolvedPartitions);
            Assert.Equal([1], fixture.Assigned);
        }

        [Fact]
        public async Task ForTheSameAssignmentInEitherOrder_ReturnsTheSameResolvedPosition()
        {
            var resolutions = 0;
            var fixture = await Start((_, _) => Task.FromResult(StartPosition.Offset(++resolutions * 10)));

            var checkpointFirst = await fixture.ProcessorFactory.Processor.Assign("0");
            var initializeFirst = await fixture.ProcessorFactory.Processor.AssignInitializingFirst("1");

            Assert.Equal(EventPosition.FromSequenceNumber(9, isInclusive: false), checkpointFirst);
            Assert.Equal(EventPosition.FromSequenceNumber(19, isInclusive: false), initializeFirst);
        }

        [Fact]
        public async Task ForANewAssignmentOfTheSamePartition_ResolvesTheStartPositionAgain()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("1");
            await fixture.ProcessorFactory.Processor.Lose("1");

            await fixture.ProcessorFactory.Processor.Assign("1");

            Assert.Equal([1, 1], fixture.ResolvedPartitions);
        }

        [Fact]
        public async Task ForAPartitionThatIsNotANumber_Throws()
        {
            var fixture = await Start();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ProcessorFactory.Processor.Assign("partition-a"));

            Assert.Contains("'partition-a'", exception.Message);
        }

        [Fact]
        public async Task ForAPartitionBeyondTheDeclaredCount_Throws()
        {
            var fixture = await Start(partitionCount: 2);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ProcessorFactory.Processor.Assign("2"));

            Assert.Contains("'2'", exception.Message);
            Assert.Contains("0 to 1", exception.Message);
        }

        [Fact]
        public async Task WhenResolvingFails_ThrowsLogsAndRaisesNoAssignment()
        {
            var failure = new InvalidOperationException("checkpoint store unreachable");
            var fixture = await Start((_, _) => Task.FromException<StartPosition>(failure));

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ProcessorFactory.Processor.Assign("1"));

            Assert.Same(failure, thrown);
            Assert.Empty(fixture.Assigned);
            Assert.Contains(fixture.Logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("partition 1") && entry.Message.Contains("'readings'"));
        }
    }

    public class InitializePartition
    {
        [Fact]
        public async Task RaisesPartitionAssignedWithTheMappedPartitionNumber()
        {
            var fixture = await Start();

            await fixture.ProcessorFactory.Processor.Assign("3");

            Assert.Equal([3], fixture.Assigned);
        }

        [Fact]
        public async Task AssignsAnIndependentProcessorPerPartition()
        {
            var fixture = await Start();

            await fixture.ProcessorFactory.Processor.Assign("0");
            await fixture.ProcessorFactory.Processor.Assign("1");

            Assert.NotSame(fixture.Processors[0], fixture.Processors[1]);
        }

        [Fact]
        public async Task ResolvesTheStartPositionBeforeTheProcessorExists()
        {
            var resolution = new TaskCompletionSource<StartPosition>();
            var fixture = await Start((_, _) => resolution.Task);

            var initialize = fixture.ProcessorFactory.Processor.Callbacks.InitializePartition("0", CancellationToken.None);

            Assert.Empty(fixture.Assigned);

            resolution.SetResult(StartPosition.First());
            await initialize.WaitAsync(TestTimeout);

            Assert.Equal([0], fixture.Assigned);
        }

        [Fact]
        public async Task WhenInitializedAgainWithoutBeingLost_RevokesTheLiveProcessorBeforeAssigningTheNewOne()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("1");
            var firstProcessor = fixture.Processors[1];

            await fixture.ProcessorFactory.Processor.Assign("1");

            Assert.Equal([1], fixture.Revoked);
            Assert.Equal([1, 1], fixture.Assigned);
            Assert.NotSame(firstProcessor, fixture.Processors[1]);
        }

        [Fact]
        public async Task WithoutSubscribers_Completes()
        {
            var processorFactory = new FakeEventHubsProcessorFactory();
            var consumer = new EventHubsPartitionedStreamConsumer<string>(
                processorFactory,
                "readings",
                "group",
                4,
                (_, _) => Task.FromResult(StartPosition.First()),
                false,
                new RecordingLogger<EventHubsPartitionedStreamConsumer<string>>(),
                TimeProvider.System);
            await consumer.StartProcessingMessages();

            await processorFactory.Processor.Assign("0");
        }
    }

    public class StopPartition
    {
        [Fact]
        public async Task WhenOwnershipIsLost_RaisesPartitionRevoked()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("2");

            await fixture.ProcessorFactory.Processor.Lose("2");

            Assert.Equal([2], fixture.Revoked);
        }

        [Fact]
        public async Task WhenOwnershipIsLost_StopsDispatchingToTheRevokedProcessor()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("2");
            var calls = 0;
            fixture.Processors[2].ProcessMessage += (_, _) =>
            {
                calls++;

                return Task.CompletedTask;
            };
            await fixture.ProcessorFactory.Processor.Lose("2");

            await fixture.ProcessorFactory.Processor.Deliver("2", TestEvents.Create(0));

            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task WhenOwnershipIsLostWhileHalted_ReleasesTheBlockedBatchCallback()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("2");
            await fixture.Processors[2].StopProcessing(CancellationToken.None);
            var blocked = fixture.ProcessorFactory.Processor.Deliver("2", TestEvents.Create(0));

            Assert.False(blocked.IsCompleted);

            await fixture.ProcessorFactory.Processor.Lose("2");

            await blocked.WaitAsync(TestTimeout);
        }

        [Fact]
        public async Task WhenOwnershipIsLostWithoutContainer_LogsAWarningNamingTheConsumerGroupAndTheFix()
        {
            var fixture = await Start(sharesPartitionOwnership: false);
            await fixture.ProcessorFactory.Processor.Assign("2");

            await fixture.ProcessorFactory.Processor.Lose("2");

            var entry = Assert.Single(fixture.Logger.Entries, entry => entry.Level == LogLevel.Warning);
            Assert.Contains("consumer group 'group'", entry.Message);
            Assert.Contains("partitionOwnershipContainer", entry.Message);
            Assert.Contains("possibly", entry.Message);
        }

        [Fact]
        public async Task WhenOwnershipIsLostWithContainer_LogsInformationOnly()
        {
            var fixture = await Start(sharesPartitionOwnership: true);
            await fixture.ProcessorFactory.Processor.Assign("2");

            await fixture.ProcessorFactory.Processor.Lose("2");

            Assert.DoesNotContain(fixture.Logger.Entries, entry => entry.Level >= LogLevel.Warning);
            Assert.Contains(fixture.Logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("consumer group 'group'"));
        }

        [Fact]
        public async Task OnShutdown_DoesNotRaisePartitionRevokedButReleasesTheBlockedBatchCallback()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("2");
            await fixture.Processors[2].StopProcessing(CancellationToken.None);
            var blocked = fixture.ProcessorFactory.Processor.Deliver("2", TestEvents.Create(0));

            await fixture.ProcessorFactory.Processor.Shutdown("2");

            await blocked.WaitAsync(TestTimeout);
            Assert.Empty(fixture.Revoked);
        }

        [Fact]
        public async Task ForAPartitionThatWasNeverAssigned_DoesNothing()
        {
            var fixture = await Start();

            await fixture.ProcessorFactory.Processor.Lose("2");

            Assert.Empty(fixture.Revoked);
        }

        [Fact]
        public async Task WhenARevokedSubscriberThrows_LogsAndStillReleases()
        {
            var processorFactory = new FakeEventHubsProcessorFactory();
            var logger = new RecordingLogger<EventHubsPartitionedStreamConsumer<string>>();
            var consumer = new EventHubsPartitionedStreamConsumer<string>(
                processorFactory,
                "readings",
                "group",
                4,
                (_, _) => Task.FromResult(StartPosition.First()),
                false,
                logger,
                TimeProvider.System);
            IMessageBusProcessor<string>? assigned = null;
            consumer.PartitionAssigned += (_, processor) =>
            {
                assigned = processor;

                return Task.CompletedTask;
            };
            consumer.PartitionRevoked += _ => throw new InvalidOperationException("revoke failed");
            await consumer.StartProcessingMessages();
            await processorFactory.Processor.Assign("0");
            await assigned!.StopProcessing(CancellationToken.None);
            var blocked = processorFactory.Processor.Deliver("0", TestEvents.Create(0));

            await processorFactory.Processor.Lose("0");

            await blocked.WaitAsync(TestTimeout);
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("revoking partition 0"));
        }
    }

    public class ProcessEvents
    {
        [Fact]
        public async Task DeliversTheEventsOnlyToTheProcessorOfTheirPartition()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("0");
            await fixture.ProcessorFactory.Processor.Assign("1");
            var partitionZero = new List<string>();
            var partitionOne = new List<string>();
            fixture.Processors[0].ProcessMessage += (message, _) =>
            {
                partitionZero.Add(message.Body);

                return Task.CompletedTask;
            };
            fixture.Processors[1].ProcessMessage += (message, _) =>
            {
                partitionOne.Add(message.Body);

                return Task.CompletedTask;
            };

            await fixture.ProcessorFactory.Processor.Deliver("0", TestEvents.Create(0, "a"), TestEvents.Create(1, "b"));
            await fixture.ProcessorFactory.Processor.Deliver("1", TestEvents.Create(0, "c"));

            Assert.Equal(["a", "b"], partitionZero);
            Assert.Equal(["c"], partitionOne);
        }

        [Fact]
        public async Task SetsTheSequenceNumberAsTheStreamOffset()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("0");
            long? offset = null;
            fixture.Processors[0].ProcessMessage += (message, _) =>
            {
                offset = message.Properties.StreamOffset;

                return Task.CompletedTask;
            };

            await fixture.ProcessorFactory.Processor.Deliver("0", TestEvents.Create(42));

            Assert.Equal(42, offset);
        }

        [Fact]
        public async Task ForAnEmptyBatch_DoesNothing()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("0");

            await fixture.ProcessorFactory.Processor.Deliver("0");

            Assert.Empty(fixture.Logger.Entries);
        }

        [Fact]
        public async Task ForAPartitionThatIsNotBeingProcessed_DropsTheEventsAndWarns()
        {
            var fixture = await Start();

            await fixture.ProcessorFactory.Processor.Deliver("3", TestEvents.Create(0), TestEvents.Create(1));

            var entry = Assert.Single(fixture.Logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("2 events", entry.Message);
            Assert.Contains("partition 3", entry.Message);
        }

        [Fact]
        public async Task WhenHalted_BlocksTheBatchCallbackOfThatPartitionOnly()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("0");
            await fixture.ProcessorFactory.Processor.Assign("1");
            var delivered = new List<string>();
            fixture.Processors[1].ProcessMessage += (message, _) =>
            {
                delivered.Add(message.Body);

                return Task.CompletedTask;
            };
            await fixture.Processors[0].StopProcessing(CancellationToken.None);

            var blocked = fixture.ProcessorFactory.Processor.Deliver("0", TestEvents.Create(0, "halted"));
            await fixture.ProcessorFactory.Processor.Deliver("1", TestEvents.Create(0, "running"));

            Assert.False(blocked.IsCompleted);
            Assert.Equal(["running"], delivered);
        }
    }

    public class HandleError
    {
        [Fact]
        public async Task WithALiveProcessor_RaisesProcessErrorOnIt()
        {
            var fixture = await Start();
            await fixture.ProcessorFactory.Processor.Assign("1");
            Exception? received = null;
            fixture.Processors[1].ProcessError += errorDetails =>
            {
                received = errorDetails.Exception;

                return Task.CompletedTask;
            };
            var exception = new InvalidOperationException("read failed");

            await fixture.ProcessorFactory.Processor.Fail(exception, "1");

            Assert.Same(exception, received);
            Assert.Empty(fixture.Logger.Entries);
        }

        [Fact]
        public async Task WithoutAPartition_LogsAnErrorNamingTheEventHub()
        {
            var fixture = await Start();

            await fixture.ProcessorFactory.Processor.Fail(new InvalidOperationException("storage unavailable"), null);

            var entry = Assert.Single(fixture.Logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Contains("'readings'", entry.Message);
        }

        [Fact]
        public async Task WithoutAPartition_LogsOnlyOncePerInterval()
        {
            var timeProvider = new ManualTimeProvider();
            var fixture = await Start(timeProvider: timeProvider);

            await fixture.ProcessorFactory.Processor.Fail(new InvalidOperationException("storage unavailable"), null);
            await fixture.ProcessorFactory.Processor.Fail(new InvalidOperationException("storage unavailable"), null);
            await fixture.ProcessorFactory.Processor.Fail(new InvalidOperationException("storage unavailable"), null);
            timeProvider.Advance(EventHubsErrorThrottle.DefaultInterval);
            await fixture.ProcessorFactory.Processor.Fail(new InvalidOperationException("storage unavailable"), null);

            Assert.Equal(2, fixture.Logger.Entries.Count);
            Assert.Contains("(2 similar errors", fixture.Logger.Entries[1].Message);
        }

        [Fact]
        public async Task ForAPartitionWithoutALiveProcessor_IsLoggedLikeAnEventHubError()
        {
            var fixture = await Start();

            await fixture.ProcessorFactory.Processor.Fail(new InvalidOperationException("read failed"), "2");

            Assert.Single(fixture.Logger.Entries);
        }
    }

    public class StopProcessing
    {
        [Fact]
        public async Task StopsTheSdkProcessorOnce()
        {
            var fixture = await Start();

            await fixture.Consumer.StopProcessing(CancellationToken.None);
            await fixture.Consumer.StopProcessing(CancellationToken.None);

            Assert.Equal(1, fixture.ProcessorFactory.Processor.Stops);
        }

        [Fact]
        public async Task BeforeStarting_Completes()
        {
            var consumer = new EventHubsPartitionedStreamConsumer<string>(
                new FakeEventHubsProcessorFactory(),
                "readings",
                "group",
                4,
                (_, _) => Task.FromResult(StartPosition.First()),
                false,
                new RecordingLogger<EventHubsPartitionedStreamConsumer<string>>(),
                TimeProvider.System);

            await consumer.StopProcessing(CancellationToken.None);
        }
    }

    public class DisposeAsync
    {
        [Fact]
        public async Task StopsTheSdkProcessor()
        {
            var fixture = await Start();

            await fixture.Consumer.DisposeAsync();

            Assert.Equal(1, fixture.ProcessorFactory.Processor.Stops);
        }

        [Fact]
        public async Task AfterStopProcessing_DoesNotStopTheSdkProcessorAgain()
        {
            var fixture = await Start();
            await fixture.Consumer.StopProcessing(CancellationToken.None);

            await fixture.Consumer.DisposeAsync();

            Assert.Equal(1, fixture.ProcessorFactory.Processor.Stops);
        }
    }
}
