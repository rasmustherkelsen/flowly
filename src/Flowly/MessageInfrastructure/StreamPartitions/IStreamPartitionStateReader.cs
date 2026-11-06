namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     Read-only view of the state of every stream partition currently owned by this process. Used by the health
///     check and the metrics, and available to other Flowly assemblies through <c>InternalsVisibleTo</c> for
///     operational actions.
/// </summary>
internal interface IStreamPartitionStateReader
{
    /// <summary>
    ///     Returns a snapshot of the status of every partition currently owned by this process.
    /// </summary>
    /// <returns>The statuses, in no particular order; empty when the process owns no stream partition.</returns>
    IReadOnlyList<StreamPartitionStatus> GetAll();

    /// <summary>
    ///     Returns the status of one partition.
    /// </summary>
    /// <param name="key">The queue, consumer and partition to look up.</param>
    /// <returns>The status, or <see langword="null" /> when this process does not currently own the partition.</returns>
    StreamPartitionStatus? Get(StreamPartitionKey key);
}
