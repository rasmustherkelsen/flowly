namespace Flowly.MessageStreams.DatabaseModel;

/// <summary>
///     A persisted <see cref="MessageStreamCheckpoint{TMessage}" /> position, keyed by
///     (<see cref="QueueName" />, <see cref="ConsumerName" />, <see cref="Partition" />).
/// </summary>
internal sealed class MessageStreamCheckpointRecord
{
    /// <summary>Sentinel <see cref="Partition" /> value used for a non-partitioned stream's single row.</summary>
    public const int NonPartitioned = -1;

    public int Id { get; set; }

    /// <summary>The resolved queue/stream name — disambiguates message types sharing a <see cref="ConsumerName" />.</summary>
    public required string QueueName { get; set; }

    /// <summary>The logical reader name (defaults to the handler type name).</summary>
    public required string ConsumerName { get; set; }

    /// <summary>The partition this row belongs to, or <see cref="NonPartitioned" /> for a non-partitioned stream.</summary>
    public int Partition { get; set; }

    /// <summary>
    ///     The last successfully processed stream offset, or <see langword="null" /> when a row has been created by
    ///     <see cref="MessageStreamCheckpoint{TMessage}.InitializeCheckpoint" /> but no batch has been saved yet —
    ///     kept distinct from offset <c>0</c>, which is a legitimate saved position.
    /// </summary>
    public long? Position { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
