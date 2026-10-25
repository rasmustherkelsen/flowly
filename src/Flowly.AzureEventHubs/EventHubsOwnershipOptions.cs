using Azure.Messaging.EventHubs.Primitives;
using Azure.Storage.Blobs;

namespace Flowly.AzureEventHubs;

internal sealed record EventHubsOwnershipOptions(BlobContainerClient? Container, TimeSpan? LoadBalancingInterval, TimeSpan? PartitionOwnershipExpiration)
{
    public bool IsShared => Container is not null;

    public static EventHubsOwnershipOptions Create(BlobContainerClient? container, TimeSpan? loadBalancingInterval, TimeSpan? partitionOwnershipExpiration)
    {
        var defaults = new EventProcessorOptions();

        if (loadBalancingInterval is { } interval && interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(loadBalancingInterval), interval, "The load balancing interval must be greater than zero.");

        if (partitionOwnershipExpiration is { } expiration && expiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(partitionOwnershipExpiration), expiration, "The partition ownership expiration must be greater than zero.");

        var effectiveInterval = loadBalancingInterval ?? defaults.LoadBalancingUpdateInterval;
        var effectiveExpiration = partitionOwnershipExpiration ?? defaults.PartitionOwnershipExpirationInterval;

        if (effectiveExpiration <= effectiveInterval)
            throw new ArgumentException(
                $"The partition ownership expiration ({effectiveExpiration}) must be greater than the load balancing interval ({effectiveInterval}), " +
                $"otherwise partitions would expire between two renewals. The defaults are {defaults.LoadBalancingUpdateInterval} and {defaults.PartitionOwnershipExpirationInterval}; " +
                "set both parameters when changing one of them past the other's default.",
                nameof(partitionOwnershipExpiration));

        return new EventHubsOwnershipOptions(container, loadBalancingInterval, partitionOwnershipExpiration);
    }

    public EventProcessorOptions ApplyTo(EventProcessorOptions processorOptions)
    {
        if (LoadBalancingInterval is { } interval)
            processorOptions.LoadBalancingUpdateInterval = interval;

        if (PartitionOwnershipExpiration is { } expiration)
            processorOptions.PartitionOwnershipExpirationInterval = expiration;

        return processorOptions;
    }

    public CheckpointStore CreateStore() => Container is { } container ? new BlobCheckpointStore(container) : new InMemoryPartitionOwnershipStore();
}
