namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     Resumes a halted stream partition owned by this process, addressed by queue, consumer and partition. Resuming is an
///     operator action only: nothing in Flowly calls it automatically, and a resumed partition never skips its held batch.
///     Available to other Flowly assemblies through <c>InternalsVisibleTo</c> for operational actions.
/// </summary>
internal interface IStreamPartitionResumer
{
    /// <summary>
    ///     Re-runs the batch a halted partition is holding and lets consumption continue from it. The batch gets a fresh set of
    ///     in-process retries; when it fails again the partition halts again.
    /// </summary>
    /// <param name="key">The queue, consumer and partition to resume.</param>
    /// <returns>Whether the partition was resumed, was not halted, or is not owned by this process.</returns>
    StreamPartitionResumeResult Resume(StreamPartitionKey key);
}
