namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     Identifies one partition owned by this process: the stream queue, the consumer that reads it and the partition
///     index (<see langword="null" /> for a non-partitioned stream).
/// </summary>
/// <param name="QueueName">The stream queue name.</param>
/// <param name="ConsumerName">The consumer name of the handler reading the stream.</param>
/// <param name="Partition">The partition index, or <see langword="null" /> for a non-partitioned stream.</param>
internal readonly record struct StreamPartitionKey(string QueueName, string ConsumerName, int? Partition);
