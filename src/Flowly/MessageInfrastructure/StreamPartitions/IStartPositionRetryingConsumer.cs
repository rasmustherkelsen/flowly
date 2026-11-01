namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     Implemented by a partitioned stream consumer that retries an unresolved start position itself, inside a bounded
///     window, before it gives up on the partition. For such a consumer a failed attempt does not mean the partition is
///     waiting: the shared service stops recording <see cref="StreamPartitionState.Waiting" /> from the failure of a single
///     <c>resolveStartPosition</c> call and records it when the consumer raises <see cref="StartPositionResolutionExhausted" />
///     instead. A consumer without this interface has no retry window, so every failed call records the state.
/// </summary>
internal interface IStartPositionRetryingConsumer
{
    /// <summary>
    ///     Raised with the partition index and the last failure when the retry window of a partition ended without a start
    ///     position, so no processing loop starts for it.
    /// </summary>
    event Action<int, Exception>? StartPositionResolutionExhausted;
}
