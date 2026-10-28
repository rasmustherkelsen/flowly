namespace Flowly.AzureEventHubs;

internal static class EventHubsOwnershipLoss
{
    public static string Describe(string eventHubName, string consumerGroup, string partitionId, bool sharesPartitionOwnership)
        => sharesPartitionOwnership
            ? $"Ownership of partition {partitionId} of event hub '{eventHubName}' in consumer group '{consumerGroup}' was lost: " +
              "another instance took it over, or reading the partition failed and it is initialized again."
            : $"Ownership of partition {partitionId} of event hub '{eventHubName}' in consumer group '{consumerGroup}' was lost, and no partitionOwnershipContainer is configured. " +
              "Another reader is possibly consuming this consumer group from another process: readers that are not coordinated take partitions from each other, " +
              "and the one that loses a partition can miss its events until the loss is noticed. Configure partitionOwnershipContainer to share partition ownership between instances. " +
              "The partition is also reported lost when reading it fails, after which it is initialized again.";
}
