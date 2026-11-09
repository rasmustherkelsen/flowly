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
    ///     <see langword="false" /> (the default) when it records the last message of a successfully processed batch.
    ///     <para>
    ///         <strong>A store must honor the flag:</strong> a save with <see langword="true" /> must write only when no position is
    ///         stored yet for the (<see cref="MessageStreamCheckpointContext.ConsumerName" />, <see cref="MessageStreamCheckpointContext.Partition" />)
    ///         pair, and must never overwrite a position that is already stored. A seed can complete late, after the reader has
    ///         started and saved a higher position (for example when its start position resolution was abandoned), and an unconditional
    ///         write would move the position backwards so that the next restart replays everything in between. The check and the write
    ///         have to be one atomic operation (a conditional insert or update), not a read followed by a write. A save with
    ///         <see langword="false" /> always overwrites.
    ///     </para>
    /// </summary>
    public bool IsBootstrapSeed { get; init; }
}
