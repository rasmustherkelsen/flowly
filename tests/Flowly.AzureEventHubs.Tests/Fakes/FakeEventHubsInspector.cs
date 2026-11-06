namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeEventHubsInspector(
    IReadOnlyCollection<string>? partitionIds = null,
    Exception? failure = null,
    bool isGeoReplicationEnabled = false,
    EventHubsPartitionState? partitionState = null,
    bool neverCompletes = false) : IEventHubsInspector
{
    public int Calls { get; private set; }

    public List<string> RequestedPartitionStates { get; } = [];

    public Task<EventHubsInfo> GetEventHubInfo(CancellationToken cancellationToken)
    {
        Calls++;

        if (neverCompletes)
            return NeverCompleting(cancellationToken);

        return failure is null
            ? Task.FromResult(new EventHubsInfo(partitionIds ?? [], isGeoReplicationEnabled))
            : Task.FromException<EventHubsInfo>(failure);
    }

    public Task<EventHubsPartitionState> GetPartitionState(string partitionId, CancellationToken cancellationToken)
    {
        RequestedPartitionStates.Add(partitionId);

        return failure is null
            ? Task.FromResult(partitionState ?? throw new InvalidOperationException("No partition state configured."))
            : Task.FromException<EventHubsPartitionState>(failure);
    }

    private static Task<EventHubsInfo> NeverCompleting(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<EventHubsInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

        return completion.Task;
    }
}
