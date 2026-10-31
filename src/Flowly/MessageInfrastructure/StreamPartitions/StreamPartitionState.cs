namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     The state of one stream partition (or of the single partition of a non-partitioned stream) owned by this process.
///     The numeric values are the values reported by the <c>flowly.message.stream.partition.state</c> gauge.
/// </summary>
internal enum StreamPartitionState
{
    /// <summary>A processing loop exists for the partition and is consuming.</summary>
    Running = 0,

    /// <summary>The start position of the partition could not be resolved, so no processing loop exists for it yet.</summary>
    Waiting = 1,

    /// <summary>The partition exhausted its in-process retries and stopped consuming; the stream offset does not advance past the failed batch.</summary>
    Halted = 2
}
