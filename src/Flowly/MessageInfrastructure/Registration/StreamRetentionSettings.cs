namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Retention and partitioning settings for a single stream queue, resolved from <see cref="StreamRetentionAttribute" />
///     and <see cref="StreamPartitionsAttribute" /> on the message contract.
/// </summary>
/// <param name="MaxAgeSeconds">The maximum age of retained messages in seconds, or <see langword="null" /> for no age limit.</param>
/// <param name="MaxLengthBytes">
///     The maximum total size of the stream in bytes, or <see langword="null" /> for no size limit. Interpreted
///     per-partition when <see cref="PartitionCount" /> is set — each partition is physically its own log.
/// </param>
/// <param name="PartitionCount">
///     The number of partitions the stream is divided into, or <see langword="null" /> for a non-partitioned
///     stream.
/// </param>
public readonly record struct StreamRetentionSettings(int? MaxAgeSeconds, long? MaxLengthBytes, int? PartitionCount = null);