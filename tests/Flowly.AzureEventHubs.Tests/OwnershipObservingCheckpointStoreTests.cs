using Azure.Messaging.EventHubs.Primitives;
using Azure.Messaging.EventHubs.Processor;
using Flowly.AzureEventHubs.Tests.Fakes;

namespace Flowly.AzureEventHubs.Tests;

public class OwnershipObservingCheckpointStoreTests
{
    private const string Namespace = "example.servicebus.windows.net";
    private const string Self = "this-instance";
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Expiration = TimeSpan.FromSeconds(8);

    private sealed record Fixture(
        OwnershipObservingCheckpointStore Store,
        FakeCheckpointStore Inner,
        FakeEventHubsPartitionCallbacks Callbacks,
        FixedUtcTimeProvider TimeProvider);

    private static Fixture Create()
    {
        var inner = new FakeCheckpointStore();
        var callbacks = new FakeEventHubsPartitionCallbacks();
        var timeProvider = new FixedUtcTimeProvider(Now);

        return new Fixture(new OwnershipObservingCheckpointStore(inner, Self, Expiration, callbacks, timeProvider), inner, callbacks, timeProvider);
    }

    private static EventProcessorPartitionOwnership Ownership(string partitionId, string owner, TimeSpan age)
        => new()
        {
            FullyQualifiedNamespace = Namespace,
            EventHubName = "readings",
            ConsumerGroup = "group",
            PartitionId = partitionId,
            OwnerIdentifier = owner,
            LastModifiedTime = Now - age,
            Version = "v"
        };

    public class ListOwnershipAsync
    {
        [Fact]
        public async Task ReturnsTheOwnershipOfTheWrappedStoreUnchanged()
        {
            var fixture = Create();
            fixture.Inner.Ownerships.Add(Ownership("0", "other", TimeSpan.Zero));
            fixture.Inner.Ownerships.Add(Ownership("1", Self, TimeSpan.Zero));

            var ownerships = (await fixture.Store.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None)).ToList();

            Assert.Equal(["0", "1"], ownerships.Select(ownership => ownership.PartitionId));
        }

        [Fact]
        public async Task ReportsAPartitionOwnedByAnotherActiveInstance()
        {
            var fixture = Create();
            fixture.Inner.Ownerships.Add(Ownership("1", "other", TimeSpan.FromSeconds(1)));

            await fixture.Store.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None);

            Assert.Equal(["1"], fixture.Callbacks.OwnedByAnotherInstance);
        }

        [Fact]
        public async Task DoesNotReportAPartitionOwnedByThisInstance()
        {
            var fixture = Create();
            fixture.Inner.Ownerships.Add(Ownership("1", Self, TimeSpan.FromSeconds(1)));

            await fixture.Store.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None);

            Assert.Empty(fixture.Callbacks.OwnedByAnotherInstance);
        }

        [Fact]
        public async Task MatchesTheOwnerIdentifierExactly()
        {
            var fixture = Create();
            fixture.Inner.Ownerships.Add(Ownership("1", Self.ToUpperInvariant(), TimeSpan.FromSeconds(1)));
            fixture.Inner.Ownerships.Add(Ownership("2", $"{Self}-2", TimeSpan.FromSeconds(1)));

            await fixture.Store.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None);

            Assert.Equal(["1", "2"], fixture.Callbacks.OwnedByAnotherInstance);
        }

        [Fact]
        public async Task DoesNotReportAnExpiredOwnershipOfAnotherInstance()
        {
            var fixture = Create();
            fixture.Inner.Ownerships.Add(Ownership("1", "other", Expiration));
            fixture.Inner.Ownerships.Add(Ownership("2", "other", Expiration + TimeSpan.FromSeconds(1)));

            await fixture.Store.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None);

            Assert.Empty(fixture.Callbacks.OwnedByAnotherInstance);
        }

        [Fact]
        public async Task ReportsAnOwnershipThatIsJustInsideTheExpiration()
        {
            var fixture = Create();
            fixture.Inner.Ownerships.Add(Ownership("1", "other", Expiration - TimeSpan.FromMilliseconds(1)));

            await fixture.Store.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None);

            Assert.Equal(["1"], fixture.Callbacks.OwnedByAnotherInstance);
        }

        [Fact]
        public async Task ReportsOnEveryListingWhileTheOtherInstanceKeepsThePartition()
        {
            var fixture = Create();
            fixture.Inner.Ownerships.Add(Ownership("1", "other", TimeSpan.FromSeconds(1)));

            await fixture.Store.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None);
            await fixture.Store.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None);

            Assert.Equal(["1", "1"], fixture.Callbacks.OwnedByAnotherInstance);
        }

        [Fact]
        public async Task UsesTheCurrentTimeOfEveryListing()
        {
            var fixture = Create();
            fixture.Inner.Ownerships.Add(Ownership("1", "other", TimeSpan.FromSeconds(1)));
            await fixture.Store.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None);

            fixture.TimeProvider.UtcNow = Now + Expiration;
            await fixture.Store.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None);

            Assert.Equal(["1"], fixture.Callbacks.OwnedByAnotherInstance);
        }
    }

    public class ClaimOwnershipAsync
    {
        [Fact]
        public async Task ForwardsTheClaimToTheWrappedStore()
        {
            var fixture = Create();
            var desired = Ownership("1", Self, TimeSpan.Zero);

            var claimed = (await fixture.Store.ClaimOwnershipAsync([desired], CancellationToken.None)).ToList();

            Assert.Same(desired, Assert.Single(fixture.Inner.Claimed));
            Assert.Same(desired, Assert.Single(claimed));
            Assert.Empty(fixture.Callbacks.OwnedByAnotherInstance);
        }
    }

    public class GetCheckpointAsync
    {
        [Fact]
        public async Task ForwardsTheReadToTheWrappedStore()
        {
            var fixture = Create();

            await fixture.Store.GetCheckpointAsync(Namespace, "readings", "group", "3", CancellationToken.None);

            Assert.Equal(["3"], fixture.Inner.CheckpointReads);
        }
    }

    public class UpdateCheckpointAsync
    {
        [Fact]
        public async Task ForwardsTheUpdateToTheWrappedStore()
        {
            var fixture = Create();

            await fixture.Store.UpdateCheckpointAsync(Namespace, "readings", "group", "3", Self, new CheckpointPosition("5", 5), CancellationToken.None);

            Assert.Equal(["3"], fixture.Inner.CheckpointUpdates);
        }
    }

    public class Inner
    {
        [Fact]
        public void IsTheWrappedStore()
        {
            var fixture = Create();

            Assert.Same(fixture.Inner, fixture.Store.Inner);
        }
    }
}
