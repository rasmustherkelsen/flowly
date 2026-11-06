using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.Tests.Fakes;

namespace Flowly.Tests.MessageInfrastructure.StreamPartitions;

public class StreamPartitionStateReporterTests
{
    private static (StreamPartitionStateReporter Reporter, StreamPartitionStateRegistry Registry, RecordingHandlerInstrumentation Instrumentation) Create()
    {
        var registry = new StreamPartitionStateRegistry(TimeProvider.System);
        var instrumentation = new RecordingHandlerInstrumentation();

        return (new StreamPartitionStateReporter(registry, instrumentation, "OrderHandler", "orders", "OrderConsumer"), registry, instrumentation);
    }

    private static StreamPartitionKey Key(int? partition) => new("orders", "OrderConsumer", partition);

    public class Running
    {
        [Fact]
        public void RecordsTheRunningStateAndCountsTheTransition()
        {
            var (streamPartitionStateReporter, registry, instrumentation) = Create();

            streamPartitionStateReporter.Running(3);

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(3))!.State);
            Assert.Equal([new StreamPartitionTransition("OrderHandler", "orders", "OrderConsumer", 3, StreamPartitionState.Running)], instrumentation.Transitions);
        }

        [Fact]
        public void ForANonPartitionedStream_UsesANullPartition()
        {
            var (streamPartitionStateReporter, registry, _) = Create();

            streamPartitionStateReporter.Running(null);

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(null))!.State);
        }

        [Fact]
        public void WhenAlreadyRunning_DoesNotCountAnotherTransition()
        {
            var (streamPartitionStateReporter, _, instrumentation) = Create();
            streamPartitionStateReporter.Running(3);

            streamPartitionStateReporter.Running(3);

            Assert.Single(instrumentation.Transitions);
        }
    }

    public class Waiting
    {
        [Fact]
        public void RecordsTheWaitingStateWithTheErrorAndCountsTheTransition()
        {
            var (streamPartitionStateReporter, registry, instrumentation) = Create();

            streamPartitionStateReporter.Waiting(3, new TimeoutException("store down"));

            var status = registry.Get(Key(3))!;
            Assert.Equal(StreamPartitionState.Waiting, status.State);
            Assert.Equal("store down", status.LastError!.Message);
            Assert.Equal(StreamPartitionState.Waiting, instrumentation.Transitions.Single().State);
        }

        [Fact]
        public void RepeatedFailures_CountOneTransitionAndKeepTheLatestError()
        {
            var (streamPartitionStateReporter, registry, instrumentation) = Create();

            streamPartitionStateReporter.Waiting(3, new TimeoutException("first"));
            streamPartitionStateReporter.Waiting(3, new TimeoutException("second"));

            Assert.Single(instrumentation.Transitions);
            Assert.Equal("second", registry.Get(Key(3))!.LastError!.Message);
        }
    }

    public class Halted
    {
        [Fact]
        public void OnTheSessionThatStartedTheEntry_RecordsHaltedWithTheErrorAndCountsTheTransition()
        {
            var (streamPartitionStateReporter, registry, instrumentation) = Create();
            var session = streamPartitionStateReporter.Running(3);

            session.Halted(new InvalidOperationException("poison"));

            var status = registry.Get(Key(3))!;
            Assert.Equal(StreamPartitionState.Halted, status.State);
            Assert.Equal("poison", status.LastError!.Message);
            Assert.Equal([StreamPartitionState.Running, StreamPartitionState.Halted], instrumentation.Transitions.Select(transition => transition.State));
        }

        [Fact]
        public void AfterTheEntryWasRevoked_DoesNotRecreateItOrCountATransition()
        {
            var (streamPartitionStateReporter, registry, instrumentation) = Create();
            var session = streamPartitionStateReporter.Running(3);
            streamPartitionStateReporter.Revoked(3);

            session.Halted(new InvalidOperationException("poison"));

            Assert.Null(registry.Get(Key(3)));
            Assert.Equal([StreamPartitionState.Running], instrumentation.Transitions.Select(transition => transition.State));
        }

        [Fact]
        public void AfterTheEntryWasReplacedBySuccessor_DoesNotOverwriteTheSuccessor()
        {
            var (streamPartitionStateReporter, registry, _) = Create();
            var oldSession = streamPartitionStateReporter.Running(3);
            streamPartitionStateReporter.Revoked(3);
            streamPartitionStateReporter.Running(3);

            oldSession.Halted(new InvalidOperationException("poison"));

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(3))!.State);
        }
    }

    public class Revoked
    {
        [Fact]
        public void RemovesTheEntryWithoutCountingATransition()
        {
            var (streamPartitionStateReporter, registry, instrumentation) = Create();
            streamPartitionStateReporter.Running(3);

            streamPartitionStateReporter.Revoked(3);

            Assert.Null(registry.Get(Key(3)));
            Assert.Single(instrumentation.Transitions);
        }

        [Fact]
        public void WithTheVersionOfTheWaitingUpdate_RemovesTheWaitingEntry()
        {
            var (streamPartitionStateReporter, registry, _) = Create();
            var update = streamPartitionStateReporter.Waiting(3, new TimeoutException("store down"));

            streamPartitionStateReporter.Revoked(3, update.Version);

            Assert.Null(registry.Get(Key(3)));
        }

        [Fact]
        public void WithTheVersionOfAnOlderWaitingUpdate_KeepsTheEntryOfTheNewerRunningLoop()
        {
            var (streamPartitionStateReporter, registry, _) = Create();
            var update = streamPartitionStateReporter.Waiting(3, new TimeoutException("store down"));
            streamPartitionStateReporter.Running(3);

            streamPartitionStateReporter.Revoked(3, update.Version);

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(3))!.State);
        }
    }

    public class Stopped
    {
        [Fact]
        public void RemovesEveryPartitionOfTheRegistration()
        {
            var (streamPartitionStateReporter, registry, _) = Create();
            streamPartitionStateReporter.Running(0);
            streamPartitionStateReporter.Waiting(1, new TimeoutException("store down"));

            streamPartitionStateReporter.Stopped();

            Assert.Empty(registry.GetAll());
        }
    }
}
