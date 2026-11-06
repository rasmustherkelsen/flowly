using Azure.Messaging.EventHubs.Consumer;
using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.Transport;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsStreamProcessorTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private static async Task<(EventHubsStreamProcessor<string> Processor, FakeEventHubsProcessorFactory ProcessorFactory)> Start(
        StartPosition? startPosition = null,
        bool sharesPartitionOwnership = false,
        TimeProvider? timeProvider = null)
    {
        var processorFactory = new FakeEventHubsProcessorFactory();
        var eventHubsStreamProcessor = new EventHubsStreamProcessor<string>(
            processorFactory,
            "readings",
            "group",
            startPosition ?? StartPosition.First(),
            sharesPartitionOwnership,
            timeProvider ?? TimeProvider.System);

        await eventHubsStreamProcessor.StartProcessingMessages();

        return (eventHubsStreamProcessor, processorFactory);
    }

    public class StartProcessingMessages
    {
        [Fact]
        public async Task CreatesAndStartsTheSdkProcessorForTheEventHubAndConsumerGroup()
        {
            var (_, processorFactory) = await Start();

            Assert.Equal("readings", processorFactory.Processor.EventHubName);
            Assert.Equal("group", processorFactory.Processor.ConsumerGroup);
            Assert.Equal(1, processorFactory.Processor.Starts);
        }

        [Fact]
        public async Task WhenTheSdkProcessorFailsToStart_PropagatesTheFailure()
        {
            var processorFactory = new FakeEventHubsProcessorFactory { StartFailure = new InvalidOperationException("missing consumer group") };
            var eventHubsStreamProcessor = new EventHubsStreamProcessor<string>(processorFactory, "readings", "group", StartPosition.First(), false, TimeProvider.System);

            await Assert.ThrowsAsync<InvalidOperationException>(() => eventHubsStreamProcessor.StartProcessingMessages());
        }
    }

    public class GetStartingPosition
    {
        [Fact]
        public async Task WithoutDispatchedEvents_IsTheStartPositionFlowlyResolved()
        {
            var (_, processorFactory) = await Start(StartPosition.Offset(12));

            Assert.Equal(EventPosition.FromSequenceNumber(11, isInclusive: false), await processorFactory.Processor.Callbacks.GetStartingPosition("0", CancellationToken.None));
        }

        [Fact]
        public async Task AfterEventsWereDispatched_ResumesAfterTheLastOne()
        {
            var (_, processorFactory) = await Start(StartPosition.First());
            await processorFactory.Processor.Deliver("0", TestEvents.Create(4), TestEvents.Create(5));

            Assert.Equal(EventPosition.FromSequenceNumber(5, isInclusive: false), await processorFactory.Processor.Callbacks.GetStartingPosition("0", CancellationToken.None));
        }

        [Fact]
        public async Task AfterTheFirstEventOfTheStreamWasDispatched_ResumesAfterSequenceNumberZero()
        {
            var (_, processorFactory) = await Start(StartPosition.First());
            await processorFactory.Processor.Deliver("0", TestEvents.Create(0));

            Assert.Equal(EventPosition.FromSequenceNumber(0, isInclusive: false), await processorFactory.Processor.Callbacks.GetStartingPosition("0", CancellationToken.None));
        }

        [Fact]
        public async Task ForAnotherPartition_Throws()
        {
            var (_, processorFactory) = await Start();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => processorFactory.Processor.Callbacks.GetStartingPosition("1", CancellationToken.None));

            Assert.Contains("not partitioned", exception.Message);
        }
    }

    public class InitializePartition
    {
        [Fact]
        public async Task ForTheOnlyPartition_Completes()
        {
            var (_, processorFactory) = await Start();

            await processorFactory.Processor.Callbacks.InitializePartition("0", CancellationToken.None);
        }

        [Fact]
        public async Task ForAnotherPartition_Throws()
        {
            var (_, processorFactory) = await Start();

            await Assert.ThrowsAsync<InvalidOperationException>(() => processorFactory.Processor.Callbacks.InitializePartition("1", CancellationToken.None));
        }
    }

    public class ProcessEvents
    {
        [Fact]
        public async Task DeliversTheEventsToTheSubscribersWithTheirSequenceNumbersAsOffsets()
        {
            var (eventHubsStreamProcessor, processorFactory) = await Start();
            var received = new List<(string Body, long? Offset)>();
            eventHubsStreamProcessor.ProcessMessage += (message, _) =>
            {
                received.Add((message.Body, message.Properties.StreamOffset));

                return Task.CompletedTask;
            };

            await processorFactory.Processor.Deliver("0", TestEvents.Create(10, "a"), TestEvents.Create(11, "b"));

            Assert.Equal([("a", (long?)10), ("b", (long?)11)], received);
        }

        [Fact]
        public async Task ForAnEmptyBatch_DoesNothing()
        {
            var (eventHubsStreamProcessor, processorFactory) = await Start();
            var calls = 0;
            eventHubsStreamProcessor.ProcessMessage += (_, _) =>
            {
                calls++;

                return Task.CompletedTask;
            };

            await processorFactory.Processor.Deliver("0");

            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task AfterStopProcessing_BlocksTheBatchCallbackAndKeepsTheSdkProcessorRunning()
        {
            var (eventHubsStreamProcessor, processorFactory) = await Start();
            var calls = 0;
            eventHubsStreamProcessor.ProcessMessage += (_, _) =>
            {
                calls++;

                return Task.CompletedTask;
            };
            await eventHubsStreamProcessor.StopProcessing(CancellationToken.None);

            var blocked = processorFactory.Processor.Deliver("0", TestEvents.Create(0));

            Assert.False(blocked.IsCompleted);
            Assert.Equal(0, calls);
            Assert.Equal(0, processorFactory.Processor.Stops);

            await eventHubsStreamProcessor.DisposeAsync();
            await blocked.WaitAsync(TestTimeout);
        }
    }

    public class StopPartition
    {
        [Fact]
        public async Task WhenOwnershipIsLost_RaisesProcessErrorExplainingWhatMayHaveHappened()
        {
            var (eventHubsStreamProcessor, processorFactory) = await Start(sharesPartitionOwnership: false);
            ErrorDetails? received = null;
            eventHubsStreamProcessor.ProcessError += errorDetails =>
            {
                received = errorDetails;

                return Task.CompletedTask;
            };

            await processorFactory.Processor.Lose("0");

            Assert.IsType<EventHubsOwnershipLostException>(received!.Exception);
            Assert.Contains("consumer group 'group'", received.Exception.Message);
            Assert.Contains("partitionOwnershipContainer", received.Exception.Message);
            Assert.Equal("readings", received.EndPoint);
        }

        [Fact]
        public async Task WhenOwnershipIsLostWithContainer_DoesNotSuggestConfiguringOne()
        {
            var (eventHubsStreamProcessor, processorFactory) = await Start(sharesPartitionOwnership: true);
            ErrorDetails? received = null;
            eventHubsStreamProcessor.ProcessError += errorDetails =>
            {
                received = errorDetails;

                return Task.CompletedTask;
            };

            await processorFactory.Processor.Lose("0");

            Assert.DoesNotContain("no partitionOwnershipContainer is configured", received!.Exception.Message);
        }

        [Fact]
        public async Task WhenOwnershipIsLostAndInitializedAgain_ResumesAfterTheLastDispatchedEvent()
        {
            var (_, processorFactory) = await Start(StartPosition.First());
            await processorFactory.Processor.Deliver("0", TestEvents.Create(8));
            await processorFactory.Processor.Lose("0");

            Assert.Equal(EventPosition.FromSequenceNumber(8, isInclusive: false), await processorFactory.Processor.Callbacks.GetStartingPosition("0", CancellationToken.None));
        }

        [Fact]
        public async Task OnShutdown_ReleasesAHaltedBatchCallback()
        {
            var (eventHubsStreamProcessor, processorFactory) = await Start();
            await eventHubsStreamProcessor.StopProcessing(CancellationToken.None);
            var blocked = processorFactory.Processor.Deliver("0", TestEvents.Create(0));

            await processorFactory.Processor.Shutdown("0");

            await blocked.WaitAsync(TestTimeout);
        }
    }

    public class HandleError
    {
        [Fact]
        public async Task ForAPartition_RaisesProcessError()
        {
            var (eventHubsStreamProcessor, processorFactory) = await Start();
            Exception? received = null;
            eventHubsStreamProcessor.ProcessError += errorDetails =>
            {
                received = errorDetails.Exception;

                return Task.CompletedTask;
            };
            var exception = new InvalidOperationException("read failed");

            await processorFactory.Processor.Fail(exception, "0");

            Assert.Same(exception, received);
        }

        [Fact]
        public async Task WithoutAPartition_RaisesProcessErrorOnlyOncePerInterval()
        {
            var timeProvider = new ManualTimeProvider();
            var (eventHubsStreamProcessor, processorFactory) = await Start(timeProvider: timeProvider);
            var errors = 0;
            eventHubsStreamProcessor.ProcessError += _ =>
            {
                errors++;

                return Task.CompletedTask;
            };

            await processorFactory.Processor.Fail(new InvalidOperationException("storage unavailable"), null);
            await processorFactory.Processor.Fail(new InvalidOperationException("storage unavailable"), null);
            timeProvider.Advance(EventHubsErrorThrottle.DefaultInterval);
            await processorFactory.Processor.Fail(new InvalidOperationException("storage unavailable"), null);

            Assert.Equal(2, errors);
        }
    }

    public class DisposeAsync
    {
        [Fact]
        public async Task StopsTheSdkProcessorOnce()
        {
            var (eventHubsStreamProcessor, processorFactory) = await Start();

            await eventHubsStreamProcessor.DisposeAsync();
            await eventHubsStreamProcessor.DisposeAsync();

            Assert.Equal(1, processorFactory.Processor.Stops);
        }

        [Fact]
        public async Task BeforeStarting_Completes()
        {
            var eventHubsStreamProcessor = new EventHubsStreamProcessor<string>(new FakeEventHubsProcessorFactory(), "readings", "group", StartPosition.First(), false, TimeProvider.System);

            await eventHubsStreamProcessor.DisposeAsync();
        }
    }
}
