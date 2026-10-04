namespace Flowly.ManagementApi.MessageStreams;

/// <summary>The request body for resetting a message stream checkpoint's position.</summary>
/// <param name="QueueName">The queue name of the stream to reset.</param>
/// <param name="ConsumerName">The consumer name to reset.</param>
/// <param name="Partition">The partition to reset, or <see langword="null" /> for a non-partitioned stream.</param>
/// <param name="Offset">The offset to reset the position to.</param>
/// <param name="Store">
///     The name of the persistence store to reset against, as passed to <c>Add&lt;Provider&gt;MessageStreamPersistence</c>.
///     Omit to use the default (unnamed) store.
/// </param>
public sealed record ResetCheckpointRequest(string QueueName, string ConsumerName, int? Partition, long Offset, string? Store);
