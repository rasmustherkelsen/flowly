namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     The outcome of <see cref="IStreamPartitionResumer.Resume" />.
/// </summary>
internal enum StreamPartitionResumeResult
{
    /// <summary>The partition was halted; it moved to <see cref="StreamPartitionState.Running" /> and re-runs its held batch.</summary>
    Resumed = 0,

    /// <summary>This process owns the partition, but it is not halted, so there is nothing to resume.</summary>
    NotHalted = 1,

    /// <summary>This process does not currently own the partition.</summary>
    NotOwned = 2
}
