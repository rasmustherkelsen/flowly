namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     A snapshot of the state of one stream partition owned by this process.
/// </summary>
/// <param name="HandlerName">The name of the stream handler class reading the partition.</param>
/// <param name="Key">The queue, consumer and partition the status belongs to.</param>
/// <param name="State">The current state.</param>
/// <param name="LastError">
///     The last error recorded for the partition while this process has owned it, kept when the partition returns to
///     <see cref="StreamPartitionState.Running" />; <see langword="null" /> when none was recorded.
/// </param>
/// <param name="StateChangedAt">When the partition entered <paramref name="State" />.</param>
internal sealed record StreamPartitionStatus(
    string HandlerName,
    StreamPartitionKey Key,
    StreamPartitionState State,
    StreamPartitionError? LastError,
    DateTimeOffset StateChangedAt);
