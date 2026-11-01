namespace Flowly.ManagementApi.MessageStreams;

/// <summary>The request body for resuming a halted stream partition.</summary>
/// <param name="QueueName">The stream queue name of the partition to resume.</param>
/// <param name="ConsumerName">The consumer name of the handler reading the stream.</param>
/// <param name="Partition">The partition to resume, or <see langword="null" /> for a non-partitioned stream.</param>
internal sealed record ResumeStreamPartitionRequest(string QueueName, string ConsumerName, int? Partition);
