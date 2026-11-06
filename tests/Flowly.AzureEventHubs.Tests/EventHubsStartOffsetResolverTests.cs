using Flowly.AzureEventHubs.Tests.Fakes;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsStartOffsetResolverTests
{
    private static readonly DateTimeOffset LastEnqueuedTime = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static (EventHubsStartOffsetResolver Resolver, FakeEventHubsInspector Inspector) Create(EventHubsPartitionState? partitionState = null, Exception? failure = null)
    {
        var clientFactory = new FakeEventHubsClientFactory();
        var inspector = new FakeEventHubsInspector(partitionState: partitionState, failure: failure);
        clientFactory.Inspectors["readings"] = inspector;

        return (new EventHubsStartOffsetResolver(clientFactory), inspector);
    }

    private static EventHubsPartitionState Populated() => new(5, 29, LastEnqueuedTime, false);

    private static EventHubsPartitionState Empty() => new(-1, -1, default, true);

    public class ResolveStartOffset
    {
        [Fact]
        public async Task Last_OfAPopulatedPartition_IsTheSequenceNumberAfterTheLastEnqueuedEvent()
        {
            var (eventHubsStartOffsetResolver, _) = Create(Populated());

            Assert.Equal(30, await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Last(), CancellationToken.None));
        }

        [Fact]
        public async Task Last_OfAnEmptyPartition_IsZero()
        {
            var (eventHubsStartOffsetResolver, _) = Create(Empty());

            Assert.Equal(0, await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Last(), CancellationToken.None));
        }

        [Fact]
        public async Task Last_OfAPartitionWhoseEventsAllExpired_IsTheSequenceNumberAfterTheLastEverEnqueued()
        {
            var (eventHubsStartOffsetResolver, _) = Create(new EventHubsPartitionState(100, 99, LastEnqueuedTime, true));

            Assert.Equal(100, await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Last(), CancellationToken.None));
        }

        [Fact]
        public async Task Last_ForAPartition_ReadsThatPartitionOnly()
        {
            var (eventHubsStartOffsetResolver, inspector) = Create(Populated());

            await eventHubsStartOffsetResolver.ResolveStartOffset("readings", 3, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(["3"], inspector.RequestedPartitionStates);
        }

        [Fact]
        public async Task Last_ForANonPartitionedStream_ReadsPartitionZero()
        {
            var (eventHubsStartOffsetResolver, inspector) = Create(Populated());

            await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(["0"], inspector.RequestedPartitionStates);
        }

        [Fact]
        public async Task First_OfAPopulatedPartition_IsTheBeginningSequenceNumber()
        {
            var (eventHubsStartOffsetResolver, _) = Create(Populated());

            Assert.Equal(5, await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.First(), CancellationToken.None));
        }

        [Fact]
        public async Task First_OfAnEmptyPartition_IsZero()
        {
            var (eventHubsStartOffsetResolver, _) = Create(Empty());

            Assert.Equal(0, await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.First(), CancellationToken.None));
        }

        [Fact]
        public async Task Offset_IsTheOffsetItselfWithoutReadingThePartition()
        {
            var (eventHubsStartOffsetResolver, inspector) = Create();

            Assert.Equal(17, await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Offset(17), CancellationToken.None));
            Assert.Empty(inspector.RequestedPartitionStates);
        }

        [Fact]
        public async Task NegativeOffset_IsZero()
        {
            var (eventHubsStartOffsetResolver, _) = Create();

            Assert.Equal(0, await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Offset(-4), CancellationToken.None));
        }

        [Fact]
        public async Task Timestamp_AfterTheLastEnqueuedEvent_IsTheSequenceNumberAfterIt()
        {
            var (eventHubsStartOffsetResolver, _) = Create(Populated());

            var offset = await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Timestamp(LastEnqueuedTime.UtcDateTime.AddMinutes(1)), CancellationToken.None);

            Assert.Equal(30, offset);
        }

        [Fact]
        public async Task Timestamp_BeforeTheLastEnqueuedEvent_CannotBeResolvedWithoutReading()
        {
            var (eventHubsStartOffsetResolver, _) = Create(Populated());

            var offset = await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Timestamp(LastEnqueuedTime.UtcDateTime.AddMinutes(-1)), CancellationToken.None);

            Assert.Null(offset);
        }

        [Fact]
        public async Task Timestamp_AtTheLastEnqueuedTime_CannotBeResolvedBecauseThatEventIsAtOrAfterIt()
        {
            var (eventHubsStartOffsetResolver, _) = Create(Populated());

            var offset = await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Timestamp(LastEnqueuedTime.UtcDateTime), CancellationToken.None);

            Assert.Null(offset);
        }

        [Fact]
        public async Task Timestamp_OfAnEmptyPartition_IsZero()
        {
            var (eventHubsStartOffsetResolver, _) = Create(Empty());

            var offset = await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Timestamp(LastEnqueuedTime.UtcDateTime), CancellationToken.None);

            Assert.Equal(0, offset);
        }

        [Fact]
        public async Task Timestamp_WithUnspecifiedKind_IsTreatedAsUtc()
        {
            var (eventHubsStartOffsetResolver, _) = Create(Populated());
            var justAfter = DateTime.SpecifyKind(LastEnqueuedTime.UtcDateTime.AddSeconds(1), DateTimeKind.Unspecified);

            Assert.Equal(30, await eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Timestamp(justAfter), CancellationToken.None));
        }

        [Fact]
        public async Task WhenReadingThePartitionFails_PropagatesTheFailure()
        {
            var failure = new InvalidOperationException("unreachable");
            var (eventHubsStartOffsetResolver, _) = Create(failure: failure);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => eventHubsStartOffsetResolver.ResolveStartOffset("readings", null, StartPosition.Last(), CancellationToken.None));

            Assert.Same(failure, thrown);
        }
    }
}
