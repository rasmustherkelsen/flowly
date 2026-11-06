namespace Flowly;

/// <summary>
///     Declares that the stream backing this message contract is partitioned into <see cref="Count" /> independent,
///     ordered sub-logs. Applied to the message contract, not the handler — partition count is owned by the
///     contract, the same way <see cref="QueueNameAttribute" /> and <see cref="StreamRetentionAttribute" /> are.
///     Read by both <c>AddMessageStreamHandler</c> and <c>AddMessageRecorder</c>; conflicting counts declared for the
///     same queue name throw <see cref="InvalidOperationException" /> at startup. Requires the resolved provider's
///     client to implement <see cref="Transport.IPartitionedStreamCapableMessageBusClient" /> — a transport with
///     broker-coordinated partition ownership, such as RabbitMQ, or with SDK-coordinated ownership, such as Azure Event Hubs
///     (whose event hub must have exactly the partitions <c>0</c> to <c>Count - 1</c>). InMemory implements it too, for local development and
///     testing parity, but gets none of the cross-instance scale-out benefit partitioning exists for, since InMemory is
///     inherently single-process. The declared count must match the partitions that exist on the broker: on RabbitMQ
///     a Super Stream with a different number of partition streams fails host startup (with <c>createTopology: true</c>
///     a broker with fewer partitions is first topped up, one with more fails), Flowly never deletes or re-creates
///     partition streams, and changing the count of a live stream is not supported.
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
