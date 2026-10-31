using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.Tests.Fakes;

namespace Flowly.Tests.MessageInfrastructure.StreamPartitions;

public class StreamPartitionStateSessionTests
{
    private static (StreamPartitionStateSession Session, StreamPartitionStateRegistry Registry, StreamPartitionStateReporter Reporter, RecordingHandlerInstrumentation Instrumentation) Create(int? partition)
    {
        var registry = new StreamPartitionStateRegistry(TimeProvider.System);
        var instrumentation = new RecordingHandlerInstrumentation();
        var reporter = new StreamPartitionStateReporter(registry, instrumentation, "OrderHandler", "orders", "OrderConsumer");

        return (reporter.Running(partition), registry, reporter, instrumentation);
    }

    private static StreamPartitionKey Key(int? partition) => new("orders", "OrderConsumer", partition);

    public class Halted
    {
        [Fact]
        public void RecordsHaltedWithTheError()
        {
            var (streamPartitionStateSession, registry, _, _) = Create(2);

            streamPartitionStateSession.Halted(new InvalidOperationException("boom"));

            var status = registry.Get(Key(2))!;
            Assert.Equal(StreamPartitionState.Halted, status.State);
            Assert.Equal("boom", status.LastError!.Message);
        }

        [Fact]
        public void AfterTheEntryWasReplaced_DoesNotWriteOverTheSuccessor()
        {
            var (streamPartitionStateSession, registry, reporter, _) = Create(2);
            reporter.Revoked(2);
            reporter.Running(2);

            streamPartitionStateSession.Halted(new InvalidOperationException("boom"));

            Assert.Equal(StreamPartitionState.Running, registry.Get(Key(2))!.State);
        }
    }

    public class Resumed
    {
        [Fact]
        public void AfterAHalt_RecordsRunningAndKeepsTheLastError()
        {
            var (streamPartitionStateSession, registry, _, instrumentation) = Create(null);
            streamPartitionStateSession.Halted(new InvalidOperationException("boom"));

            streamPartitionStateSession.Resumed();

            var status = registry.Get(Key(null))!;
            Assert.Equal(StreamPartitionState.Running, status.State);
            Assert.Equal("boom", status.LastError!.Message);
            Assert.Equal([StreamPartitionState.Running, StreamPartitionState.Halted, StreamPartitionState.Running], instrumentation.Transitions.Select(transition => transition.State));
        }

        [Fact]
        public void AfterSeveralHaltsAndResumes_StillAppliesEveryUpdate()
        {
            var (streamPartitionStateSession, registry, _, _) = Create(1);

            streamPartitionStateSession.Halted(new InvalidOperationException("first"));
            streamPartitionStateSession.Resumed();
            streamPartitionStateSession.Halted(new InvalidOperationException("second"));

            var status = registry.Get(Key(1))!;
            Assert.Equal(StreamPartitionState.Halted, status.State);
            Assert.Equal("second", status.LastError!.Message);
        }

        [Fact]
        public void AfterTheEntryWasRemoved_DoesNotRecreateIt()
        {
            var (streamPartitionStateSession, registry, reporter, _) = Create(1);
            streamPartitionStateSession.Halted(new InvalidOperationException("boom"));
            reporter.Revoked(1);

            streamPartitionStateSession.Resumed();

            Assert.Null(registry.Get(Key(1)));
        }
    }
}
