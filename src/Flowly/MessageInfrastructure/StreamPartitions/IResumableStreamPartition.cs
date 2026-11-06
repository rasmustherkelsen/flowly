namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     The resume handle of one live stream partition loop, registered in the <see cref="IStreamPartitionResumeRegistry" />
///     for as long as the loop runs.
/// </summary>
internal interface IResumableStreamPartition
{
    /// <summary>
    ///     Releases the loop's held batch so it is re-run in place, and moves the partition from
    ///     <see cref="StreamPartitionState.Halted" /> to <see cref="StreamPartitionState.Running" />.
    /// </summary>
    /// <returns><see langword="true" /> when the loop was halted and was released; <see langword="false" /> when it was not halted.</returns>
    bool Resume();
}
