namespace Flowly;

/// <summary>
///     Identifies the reader whose position a <see cref="MessageStreamCheckpoint{TMessage}" /> query applies to.
///     <see cref="ConsumerName" /> disambiguates independent readers of the same stream — it defaults to the
///     registered handler type name and is overridable via <see cref="MessageStreamHandlerOptions.ConsumerName" />.
///     <see cref="Partition" /> is <see langword="null" /> for a non-partitioned stream.
/// </summary>
/// <param name="ConsumerName">The logical name of the reader this position belongs to.</param>
/// <param name="Partition">The partition this position belongs to, or <see langword="null" /> for a non-partitioned stream.</param>
public record MessageStreamCheckpointContext(string ConsumerName, int? Partition);

/// <summary>
///     A <see cref="MessageStreamCheckpointContext" /> carrying the position to persist, passed to
///     <see cref="MessageStreamCheckpoint{TMessage}.SaveStreamPosition" />.
/// </summary>
/// <param name="ConsumerName">The logical name of the reader this position belongs to.</param>
/// <param name="Partition">The partition this position belongs to, or <see langword="null" /> for a non-partitioned stream.</param>
/// <param name="Position">The stream offset of the last successfully processed message.</param>
public sealed record MessageStreamCheckpointSaveContext(string ConsumerName, int? Partition, long Position)
    : MessageStreamCheckpointContext(ConsumerName, Partition);
