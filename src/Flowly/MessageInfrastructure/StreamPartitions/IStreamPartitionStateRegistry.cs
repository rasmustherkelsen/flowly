namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     The store of stream partition states, written by the shared stream services and read through
///     <see cref="IStreamPartitionStateReader" />. A state is written on a transition, never per message.
/// </summary>
internal interface IStreamPartitionStateRegistry : IStreamPartitionStateReader
{
    /// <summary>
    ///     Records <paramref name="state" /> for a partition. A change of state resets
    ///     <see cref="StreamPartitionStatus.StateChangedAt" />; repeating the current state only refreshes the error.
    /// </summary>
    /// <param name="handlerName">The name of the stream handler class reading the partition.</param>
    /// <param name="key">The queue, consumer and partition.</param>
    /// <param name="state">The new state.</param>
    /// <param name="error">The error that caused the state, or <see langword="null" /> to keep the last recorded error.</param>
    /// <param name="expectedVersion">
    ///     When set, the update is applied only if the partition's current entry still has this version, so a loop that was
    ///     revoked or replaced cannot write over the state of its successor.
    /// </param>
    /// <returns>Whether the update was applied, whether the state changed, and the entry's version after the update.</returns>
    StreamPartitionUpdate Set(string handlerName, StreamPartitionKey key, StreamPartitionState state, Exception? error, long? expectedVersion = null);

    /// <summary>
    ///     Removes the entry of a partition this process no longer owns.
    /// </summary>
    /// <param name="key">The queue, consumer and partition.</param>
    /// <param name="expectedVersion">
    ///     When set, the entry is removed only if it still has this version, so a late revoke cannot delete the entry a newer
    ///     assignment of the partition has written.
    /// </param>
    void Remove(StreamPartitionKey key, long? expectedVersion = null);

    /// <summary>
    ///     Removes the entries of every partition of one consumer of one stream queue, used when its service stops.
    /// </summary>
    /// <param name="queueName">The stream queue name.</param>
    /// <param name="consumerName">The consumer name.</param>
    void RemoveAll(string queueName, string consumerName);
}
