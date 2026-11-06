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
/// <param name="Position">
///     The stream offset to store, meaning "resume at <c>Position + 1</c>". For a saved batch this is the offset of the last
///     successfully processed message. For a <see cref="IsBootstrapSeed">bootstrap seed</see> it is the first offset the reader will
///     consume minus one, so it can be <c>-1</c> (start at offset 0, for example on an empty stream). A store must accept
///     negative values.
/// </param>
public sealed record MessageStreamCheckpointSaveContext(string ConsumerName, int? Partition, long Position)
    : MessageStreamCheckpointContext(ConsumerName, Partition)
{
    /// <summary>
    ///     <see langword="true" /> when Flowly saves <see cref="MessageStreamCheckpointSaveContext.Position" /> to fix the start position of a reader that has
    ///     no stored position yet (a bootstrap seed), without any batch having been processed;
    ///     <see langword="false" /> (the default) when it records the last message of a successfully processed batch. A store
    ///     that has no use for the difference can ignore this flag and store the position either way; one that, for example,
    ///     keeps an audit trail of processed batches can use it to tell the two apart.
    /// </summary>
    public bool IsBootstrapSeed { get; init; }
}
