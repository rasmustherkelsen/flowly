namespace Flowly;

/// <summary>
///     Declares that the stream backing this message contract is partitioned into <see cref="Count" /> independent,
///     ordered sub-logs. Applied to the message contract, not the handler — partition count is owned by the
///     contract, the same way <see cref="QueueNameAttribute" /> and <see cref="StreamRetentionAttribute" /> are.
///     Read by both <c>AddMessageStreamHandler</c> and <c>AddMessageRecorder</c>; conflicting counts declared for the
///     same queue name throw <see cref="InvalidOperationException" /> at startup. Requires the resolved provider's
///     client to implement <see cref="Transport.IPartitionedStreamCapableMessageBusClient" /> — currently RabbitMQ
///     only. InMemory implements it too, for local development and testing parity, but gets none of the
///     cross-instance scale-out benefit partitioning exists for, since InMemory is inherently single-process.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StreamPartitionsAttribute : Attribute
{
    /// <param name="count">The number of partitions. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is less than 1.</exception>
    public StreamPartitionsAttribute(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Stream partition count must be at least 1.");

        Count = count;
    }

    /// <summary>
    ///     The number of independent partitions the stream is divided into.
    /// </summary>
    public int Count { get; }
}
