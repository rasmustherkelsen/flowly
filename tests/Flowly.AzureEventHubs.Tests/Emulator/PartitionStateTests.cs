using Flowly.AzureEventHubs.Tests.Emulator.Support;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public class PartitionStateTests
{
    private const int NeverWrittenPartition = 1;

    [Collection(EmulatorCollection.Name)]
    public class EventHubInfo
    {
        [EmulatorFact]
        public async Task ListsTheDeclaredPartitionIdsAndReportsNoGeoReplication()
        {
            await using var transport = EmulatorTransport.Create();

            var partitioned = await transport.EventHubInfo(EmulatorEntities.Partitioned);
            var single = await transport.EventHubInfo(EmulatorEntities.Single);

            Assert.Equal(["0", "1", "2", "3"], partitioned.PartitionIds.Order());
            Assert.Equal(["0"], single.PartitionIds);
            Assert.False(partitioned.IsGeoReplicationEnabled);
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class PartitionState
    {
        [EmulatorFact]
        public async Task OfAnEmptyPartition_ReportsMinusOneAndTheUnixEpoch()
        {
            await using var transport = EmulatorTransport.Create();

            var state = await transport.PartitionState(EmulatorEntities.Empty, NeverWrittenPartition);

            Assert.True(state.IsEmpty);
            Assert.Equal(-1, state.BeginningSequenceNumber);
            Assert.Equal(-1, state.LastEnqueuedSequenceNumber);
            Assert.Equal(DateTimeOffset.UnixEpoch, state.LastEnqueuedTime);
        }

        [EmulatorFact]
        public async Task OfAWrittenPartition_ReportsTheLastSequenceNumberAndEnqueuedTime()
        {
            await using var transport = EmulatorTransport.Create();
            await transport.Record(EmulatorEntities.Single, new SingleEvent(Guid.NewGuid().ToString(), 1));

            var state = await transport.PartitionState(EmulatorEntities.Single, 0);

            Assert.False(state.IsEmpty);
            Assert.True(state.LastEnqueuedSequenceNumber >= state.BeginningSequenceNumber);
            Assert.True(state.LastEnqueuedTime > DateTimeOffset.UtcNow.AddMinutes(-1));
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class ResolveStartOffset
    {
        [EmulatorFact]
        public async Task OnAnEmptyPartition_ResolvesEveryKindToTheStartOfThePartition()
        {
            await using var transport = EmulatorTransport.Create();

            Assert.Equal<long?>(0, await transport.ResolveStartOffset(EmulatorEntities.Empty, NeverWrittenPartition, StartPosition.First()));
            Assert.Equal<long?>(0, await transport.ResolveStartOffset(EmulatorEntities.Empty, NeverWrittenPartition, StartPosition.Last()));
            Assert.Equal<long?>(0, await transport.ResolveStartOffset(EmulatorEntities.Empty, NeverWrittenPartition, StartPosition.Timestamp(DateTime.UtcNow)));
            Assert.Equal<long?>(7, await transport.ResolveStartOffset(EmulatorEntities.Empty, NeverWrittenPartition, StartPosition.Offset(7)));
        }

        [EmulatorFact]
        public async Task OnAWrittenPartition_ResolvesLastToTheSequenceNumberAfterTheLastEvent()
        {
            await using var transport = EmulatorTransport.Create();
            await transport.Record(EmulatorEntities.Single, new SingleEvent(Guid.NewGuid().ToString(), 1));
            var state = await transport.PartitionState(EmulatorEntities.Single, 0);

            Assert.Equal<long?>(state.LastEnqueuedSequenceNumber + 1, await transport.ResolveStartOffset(EmulatorEntities.Single, null, StartPosition.Last()));
            Assert.Equal<long?>(state.BeginningSequenceNumber, await transport.ResolveStartOffset(EmulatorEntities.Single, null, StartPosition.First()));
        }

        [EmulatorFact]
        public async Task OnAWrittenPartition_ResolvesATimestampOnlyWhenItIsLaterThanTheLastEvent()
        {
            await using var transport = EmulatorTransport.Create();
            await transport.Record(EmulatorEntities.Single, new SingleEvent(Guid.NewGuid().ToString(), 1));
            var state = await transport.PartitionState(EmulatorEntities.Single, 0);

            Assert.Equal<long?>(state.LastEnqueuedSequenceNumber + 1, await transport.ResolveStartOffset(EmulatorEntities.Single, null, StartPosition.Timestamp(state.LastEnqueuedTime.UtcDateTime.AddSeconds(1))));
            Assert.Null(await transport.ResolveStartOffset(EmulatorEntities.Single, null, StartPosition.Timestamp(state.LastEnqueuedTime.UtcDateTime.AddSeconds(-1))));
        }
    }
}
