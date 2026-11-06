using Azure.Messaging.EventHubs.Primitives;
using Azure.Messaging.EventHubs.Processor;

namespace Flowly.AzureEventHubs.Tests;

public class InMemoryPartitionOwnershipStoreTests
{
    private const string Namespace = "example.servicebus.windows.net";

    private static EventProcessorPartitionOwnership Desired(string partitionId, string owner, string? version = null, string eventHubName = "readings", string consumerGroup = "group")
        => new()
        {
            FullyQualifiedNamespace = Namespace,
            EventHubName = eventHubName,
            ConsumerGroup = consumerGroup,
            PartitionId = partitionId,
            OwnerIdentifier = owner,
            Version = version
        };

    public class ClaimOwnershipAsync
    {
        [Fact]
        public async Task OfAnUnownedPartition_GrantsItWithAVersion()
        {
            var inMemoryPartitionOwnershipStore = new InMemoryPartitionOwnershipStore();

            var claimed = (await inMemoryPartitionOwnershipStore.ClaimOwnershipAsync([Desired("0", "a")], CancellationToken.None)).ToList();

            var ownership = Assert.Single(claimed);
            Assert.Equal("a", ownership.OwnerIdentifier);
            Assert.False(string.IsNullOrEmpty(ownership.Version));
        }

        [Fact]
        public async Task WithTheCurrentVersion_TransfersTheOwnership()
        {
            var inMemoryPartitionOwnershipStore = new InMemoryPartitionOwnershipStore();
            var first = (await inMemoryPartitionOwnershipStore.ClaimOwnershipAsync([Desired("0", "a")], CancellationToken.None)).Single();

            var second = (await inMemoryPartitionOwnershipStore.ClaimOwnershipAsync([Desired("0", "b", first.Version)], CancellationToken.None)).ToList();

            Assert.Equal("b", Assert.Single(second).OwnerIdentifier);
            Assert.NotEqual(first.Version, second[0].Version);
        }

        [Fact]
        public async Task WithAStaleVersion_IsNotGranted()
        {
            var inMemoryPartitionOwnershipStore = new InMemoryPartitionOwnershipStore();
            await inMemoryPartitionOwnershipStore.ClaimOwnershipAsync([Desired("0", "a")], CancellationToken.None);

            var claimed = await inMemoryPartitionOwnershipStore.ClaimOwnershipAsync([Desired("0", "b", "stale")], CancellationToken.None);

            Assert.Empty(claimed);
        }

        [Fact]
        public async Task OfSeveralPartitions_GrantsOnlyTheOnesWithoutAConflict()
        {
            var inMemoryPartitionOwnershipStore = new InMemoryPartitionOwnershipStore();
            await inMemoryPartitionOwnershipStore.ClaimOwnershipAsync([Desired("0", "a")], CancellationToken.None);

            var claimed = (await inMemoryPartitionOwnershipStore.ClaimOwnershipAsync([Desired("0", "b", "stale"), Desired("1", "b")], CancellationToken.None)).ToList();

            Assert.Equal("1", Assert.Single(claimed).PartitionId);
        }

        [Fact]
        public async Task ForTheSamePartitionIdOnAnotherEventHub_DoesNotConflict()
        {
            var inMemoryPartitionOwnershipStore = new InMemoryPartitionOwnershipStore();
            await inMemoryPartitionOwnershipStore.ClaimOwnershipAsync([Desired("0", "a", eventHubName: "readings")], CancellationToken.None);

            var claimed = await inMemoryPartitionOwnershipStore.ClaimOwnershipAsync([Desired("0", "b", eventHubName: "alerts")], CancellationToken.None);

            Assert.Single(claimed);
        }
    }

    public class ListOwnershipAsync
    {
        [Fact]
        public async Task ReturnsOnlyTheOwnershipOfTheEventHubAndConsumerGroup()
        {
            var inMemoryPartitionOwnershipStore = new InMemoryPartitionOwnershipStore();
            await inMemoryPartitionOwnershipStore.ClaimOwnershipAsync(
                [Desired("0", "a"), Desired("1", "a"), Desired("0", "a", eventHubName: "alerts"), Desired("0", "a", consumerGroup: "other")],
                CancellationToken.None);

            var ownerships = (await inMemoryPartitionOwnershipStore.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None)).ToList();

            Assert.Equal(["0", "1"], ownerships.Select(ownership => ownership.PartitionId).Order());
        }

        [Fact]
        public async Task WithoutAnyClaim_ReturnsNothing()
        {
            var inMemoryPartitionOwnershipStore = new InMemoryPartitionOwnershipStore();

            Assert.Empty(await inMemoryPartitionOwnershipStore.ListOwnershipAsync(Namespace, "readings", "group", CancellationToken.None));
        }
    }

    public class GetCheckpointAsync
    {
        [Fact]
        public async Task ReturnsNothingBecauseFlowlyKeepsThePositionsItself()
        {
            var inMemoryPartitionOwnershipStore = new InMemoryPartitionOwnershipStore();

            Assert.Null(await inMemoryPartitionOwnershipStore.GetCheckpointAsync(Namespace, "readings", "group", "0", CancellationToken.None));
        }
    }

    public class UpdateCheckpointAsync
    {
        [Fact]
        public async Task IsNotSupported()
        {
            var inMemoryPartitionOwnershipStore = new InMemoryPartitionOwnershipStore();

            await Assert.ThrowsAsync<NotSupportedException>(
                () => inMemoryPartitionOwnershipStore.UpdateCheckpointAsync(Namespace, "readings", "group", "0", "client", new CheckpointPosition(), CancellationToken.None));
        }
    }
}
