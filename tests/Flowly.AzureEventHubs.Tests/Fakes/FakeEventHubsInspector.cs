namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeEventHubsInspector(IReadOnlyCollection<string>? partitionIds = null, Exception? failure = null) : IEventHubsInspector
{
    public int Calls { get; private set; }

    public Task<IReadOnlyCollection<string>> GetPartitionIds(CancellationToken cancellationToken)
    {
        Calls++;

        return failure is null
            ? Task.FromResult(partitionIds ?? [])
            : Task.FromException<IReadOnlyCollection<string>>(failure);
    }
}
