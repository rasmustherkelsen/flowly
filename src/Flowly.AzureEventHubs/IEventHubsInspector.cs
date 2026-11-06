namespace Flowly.AzureEventHubs;

internal interface IEventHubsInspector
{
    Task<EventHubsInfo> GetEventHubInfo(CancellationToken cancellationToken);

    Task<EventHubsPartitionState> GetPartitionState(string partitionId, CancellationToken cancellationToken);
}
