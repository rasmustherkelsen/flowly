namespace Flowly.AzureEventHubs;

internal interface IEventHubsInspector
{
    Task<IReadOnlyCollection<string>> GetPartitionIds(CancellationToken cancellationToken);
}
