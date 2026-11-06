namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     The store of resume handles of the stream partition loops running in this process, written by the loops themselves and
///     read through <see cref="IStreamPartitionResumer" />.
/// </summary>
internal interface IStreamPartitionResumeRegistry : IStreamPartitionResumer
{
    /// <summary>
    ///     Makes a running loop reachable through <see cref="IStreamPartitionResumer.Resume" />. A loop registered for a key that
    ///     already has one replaces it, for example when a partition is revoked and assigned again.
    /// </summary>
    /// <param name="key">The queue, consumer and partition the loop reads.</param>
    /// <param name="partition">The loop's resume handle.</param>
    /// <returns>A registration that removes the handle when disposed, unless a newer loop has replaced it.</returns>
    IDisposable Register(StreamPartitionKey key, IResumableStreamPartition partition);
}
