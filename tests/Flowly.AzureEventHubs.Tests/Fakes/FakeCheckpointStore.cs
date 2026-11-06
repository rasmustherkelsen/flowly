using Azure.Messaging.EventHubs.Primitives;
using Azure.Messaging.EventHubs.Processor;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeCheckpointStore : CheckpointStore
{
    public List<EventProcessorPartitionOwnership> Ownerships { get; } = [];

    public List<EventProcessorPartitionOwnership> Claimed { get; } = [];

    public List<string> CheckpointReads { get; } = [];

    public List<string> CheckpointUpdates { get; } = [];

    public int Lists { get; private set; }

    public override Task<IEnumerable<EventProcessorPartitionOwnership>> ListOwnershipAsync(
        string fullyQualifiedNamespace,
        string eventHubName,
        string consumerGroup,
        CancellationToken cancellationToken)
    {
        Lists++;

        return Task.FromResult<IEnumerable<EventProcessorPartitionOwnership>>([.. Ownerships]);
    }

    public override Task<IEnumerable<EventProcessorPartitionOwnership>> ClaimOwnershipAsync(
        IEnumerable<EventProcessorPartitionOwnership> desiredOwnership,
        CancellationToken cancellationToken)
    {
        var claimed = desiredOwnership.ToList();
        Claimed.AddRange(claimed);

        return Task.FromResult<IEnumerable<EventProcessorPartitionOwnership>>(claimed);
    }

    public override Task<EventProcessorCheckpoint?> GetCheckpointAsync(
        string fullyQualifiedNamespace,
        string eventHubName,
        string consumerGroup,
        string partitionId,
        CancellationToken cancellationToken)
    {
        CheckpointReads.Add(partitionId);

        return Task.FromResult<EventProcessorCheckpoint?>(null);
    }

    public override Task UpdateCheckpointAsync(
        string fullyQualifiedNamespace,
        string eventHubName,
        string consumerGroup,
        string partitionId,
        string clientIdentifier,
        CheckpointPosition position,
        CancellationToken cancellationToken)
    {
        CheckpointUpdates.Add(partitionId);

        return Task.CompletedTask;
    }
}
