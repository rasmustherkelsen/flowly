using Azure.Messaging.EventHubs.Primitives;
using Azure.Messaging.EventHubs.Processor;

namespace Flowly.AzureEventHubs;

/// <summary>
///     Wraps the shared ownership store of one SDK processor and tells <see cref="IEventHubsPartitionCallbacks.PartitionOwnedByAnotherInstance" /> about every partition
///     whose ownership record names another processor and has not expired. The SDK lists the ownership in every load balancing cycle, so a partition that was claimed by
///     another instance is reported within one cycle of the claim. All store calls are forwarded unchanged to the wrapped store.
///     <para>
///         A partition this processor never started (its start position could not be resolved) is never reported lost by the SDK, so this observation is the only signal
///         the consumer gets for it. An expired record of another processor is ignored, because the SDK treats it as free and claims it again.
///     </para>
/// </summary>
internal sealed class OwnershipObservingCheckpointStore(
    CheckpointStore inner,
    string ownerIdentifier,
    TimeSpan ownershipExpiration,
    IEventHubsPartitionCallbacks callbacks,
    TimeProvider timeProvider) : CheckpointStore
{
    public CheckpointStore Inner => inner;

    public string OwnerIdentifier => ownerIdentifier;

    public override async Task<IEnumerable<EventProcessorPartitionOwnership>> ListOwnershipAsync(
        string fullyQualifiedNamespace,
        string eventHubName,
        string consumerGroup,
        CancellationToken cancellationToken)
    {
        var ownerships = (await inner.ListOwnershipAsync(fullyQualifiedNamespace, eventHubName, consumerGroup, cancellationToken)).ToList();
        var now = timeProvider.GetUtcNow();

        foreach (var ownership in ownerships)
        {
            if (!IsActiveOwnershipOfAnotherInstance(ownership, now))
                continue;

            await callbacks.PartitionOwnedByAnotherInstance(ownership.PartitionId, cancellationToken);
        }

        return ownerships;
    }

    public override Task<IEnumerable<EventProcessorPartitionOwnership>> ClaimOwnershipAsync(
        IEnumerable<EventProcessorPartitionOwnership> desiredOwnership,
        CancellationToken cancellationToken)
        => inner.ClaimOwnershipAsync(desiredOwnership, cancellationToken);

    public override Task<EventProcessorCheckpoint?> GetCheckpointAsync(
        string fullyQualifiedNamespace,
        string eventHubName,
        string consumerGroup,
        string partitionId,
        CancellationToken cancellationToken)
        => inner.GetCheckpointAsync(fullyQualifiedNamespace, eventHubName, consumerGroup, partitionId, cancellationToken);

    public override Task UpdateCheckpointAsync(
        string fullyQualifiedNamespace,
        string eventHubName,
        string consumerGroup,
        string partitionId,
        string clientIdentifier,
        CheckpointPosition position,
        CancellationToken cancellationToken)
        => inner.UpdateCheckpointAsync(fullyQualifiedNamespace, eventHubName, consumerGroup, partitionId, clientIdentifier, position, cancellationToken);

    private bool IsActiveOwnershipOfAnotherInstance(EventProcessorPartitionOwnership ownership, DateTimeOffset now)
        => !string.Equals(ownership.OwnerIdentifier, ownerIdentifier, StringComparison.Ordinal)
           && now - ownership.LastModifiedTime < ownershipExpiration;
}
