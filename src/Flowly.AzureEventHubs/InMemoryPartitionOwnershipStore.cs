using Azure.Messaging.EventHubs.Primitives;
using Azure.Messaging.EventHubs.Processor;

namespace Flowly.AzureEventHubs;

internal sealed class InMemoryPartitionOwnershipStore : CheckpointStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, EventProcessorPartitionOwnership> _ownerships = new(StringComparer.Ordinal);

    public override Task<IEnumerable<EventProcessorPartitionOwnership>> ListOwnershipAsync(
        string fullyQualifiedNamespace,
        string eventHubName,
        string consumerGroup,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IEnumerable<EventProcessorPartitionOwnership>>(
                [.. _ownerships.Values.Where(ownership =>
                    ownership.FullyQualifiedNamespace == fullyQualifiedNamespace && ownership.EventHubName == eventHubName && ownership.ConsumerGroup == consumerGroup)]);
        }
    }

    public override Task<IEnumerable<EventProcessorPartitionOwnership>> ClaimOwnershipAsync(
        IEnumerable<EventProcessorPartitionOwnership> desiredOwnership,
        CancellationToken cancellationToken)
    {
        var claimed = new List<EventProcessorPartitionOwnership>();

        lock (_gate)
        {
            foreach (var desired in desiredOwnership)
            {
                var key = $"{desired.FullyQualifiedNamespace}/{desired.EventHubName}/{desired.ConsumerGroup}/{desired.PartitionId}";

                if (_ownerships.TryGetValue(key, out var existing) && existing.Version != desired.Version)
                    continue;

                var granted = new EventProcessorPartitionOwnership
                {
                    FullyQualifiedNamespace = desired.FullyQualifiedNamespace,
                    EventHubName = desired.EventHubName,
                    ConsumerGroup = desired.ConsumerGroup,
                    PartitionId = desired.PartitionId,
                    OwnerIdentifier = desired.OwnerIdentifier,
                    LastModifiedTime = DateTimeOffset.UtcNow,
                    Version = Guid.NewGuid().ToString("N")
                };

                _ownerships[key] = granted;
                claimed.Add(granted);
            }
        }

        return Task.FromResult<IEnumerable<EventProcessorPartitionOwnership>>(claimed);
    }

    public override Task<EventProcessorCheckpoint?> GetCheckpointAsync(
        string fullyQualifiedNamespace,
        string eventHubName,
        string consumerGroup,
        string partitionId,
        CancellationToken cancellationToken)
        => Task.FromResult<EventProcessorCheckpoint?>(null);

    public override Task UpdateCheckpointAsync(
        string fullyQualifiedNamespace,
        string eventHubName,
        string consumerGroup,
        string partitionId,
        string clientIdentifier,
        CheckpointPosition position,
        CancellationToken cancellationToken)
        => Task.FromException(new NotSupportedException("Flowly keeps stream positions in a MessageStreamCheckpoint and never writes Event Hubs checkpoints."));
}
