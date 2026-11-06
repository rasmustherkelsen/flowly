namespace Flowly.AzureEventHubs;

internal sealed record EventHubsInfo(IReadOnlyCollection<string> PartitionIds, bool IsGeoReplicationEnabled);
