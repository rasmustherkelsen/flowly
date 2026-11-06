namespace Flowly;

/// <summary>
///     The position in a message stream from which a <see cref="MessageStreamHandler{TMessage}" /> begins consuming.
///     Created via the static factory methods <see cref="First" />, <see cref="Last" />, <see cref="Offset" />, and
///     <see cref="Timestamp" /> — there is no public constructor and no default value; every stream handler must pick
///     a start position explicitly in <see cref="MessageStreamHandler{TMessage}.Configure" />.
///     <para>
///         <strong>The start position is not persisted across restarts by default.</strong> It is re-evaluated fresh on every
///         process boot unless a <see cref="MessageStreamCheckpoint{TMessage}" /> is registered and holds a stored
///         position, which then takes precedence. A value computed relative to the current time (e.g. <c>StartPosition.Timestamp(DateTime.UtcNow - TimeSpan.FromHours(2))</c>) is recomputed on every
///         restart and will never converge on a fixed point in the stream. Reprocessing already-handled messages
///         (with <see cref="First" />, <see cref="Offset" />, or <see cref="Timestamp" />) or missing messages
///         published while the process was down (with <see cref="Last" />) is an explicit operator tradeoff.
///     </para>
///     <para>
///         <strong>With a registered checkpoint, the position is fixed on the first run.</strong> A reader that has no stored
///         position yet has its <see cref="Last" /> or <see cref="Timestamp" /> resolved to a concrete offset and stored before
///         it starts consuming — when the transport can resolve it (see <see cref="Transport.IStreamStartOffsetResolver" />) — so
///         a restart before the first batch does not move it, and messages published in the meantime are delivered. A
///         <see cref="Timestamp" /> is stored as a lower bound at the broker's resolution, so a restart can redeliver messages
///         from before the time but never skips one.
///     </para>
/// </summary>
public readonly struct StartPosition : IEquatable<StartPosition>
{
    private enum Kind
    {
        First,
        Last,
        Offset,
        Timestamp
    }

    private readonly Kind _kind;
    private readonly long _offset;
    private readonly DateTime _timestamp;

    private StartPosition(Kind kind, long offset = 0, DateTime timestamp = default)
    {
        _kind = kind;
        _offset = offset;
        _timestamp = timestamp;
    }

    /// <summary>
    ///     Start consuming from the very first message retained in the stream. On every restart the handler replays the
    ///     entire retained stream from the beginning, so handlers using this position must be idempotent.
    /// </summary>
    /// <returns>A <see cref="StartPosition" /> representing the beginning of the stream.</returns>
    public static StartPosition First() => new(Kind.First);

    /// <summary>
    ///     Start consuming from the tail of the stream, receiving only messages published after the handler attaches.
    ///     Messages already in the stream when the handler attaches are never delivered, and on a stream that is empty
    ///     when the handler attaches nothing is delivered until a message is published. On a partitioned stream the
    ///     rule applies to every partition. Messages published while the process was down are not delivered after a
    ///     restart, unless a <see cref="MessageStreamCheckpoint{TMessage}" /> is registered and holds a stored position,
    ///     which takes precedence over this value.
    ///     <para>
    ///         Maps to the RabbitMQ <c>next</c> offset specification (not <c>last</c>, which attaches at the last
    ///         existing chunk and would deliver the messages in it). The InMemory transport behaves the same. With a registered
    ///         <see cref="MessageStreamCheckpoint{TMessage}" /> the tail offset at the first run is stored up front, so messages
    ///         published while the process was down are delivered after a restart even if no message had arrived before it.
    ///         Azure Event Hubs maps it to <c>EventPosition.Latest</c>.
    ///     </para>
    /// </summary>
    /// <returns>A <see cref="StartPosition" /> representing the tail of the stream.</returns>
    public static StartPosition Last() => new(Kind.Last);

    /// <summary>
    ///     Start consuming from a specific numeric offset in the stream. Offsets below the retention floor are clamped
    ///     to the first retained message by the broker. On Azure Event Hubs the offset is the sequence number of the event within its
    ///     partition (sequence numbers start at zero in every partition).
    /// </summary>
    /// <param name="offset">The zero-based stream offset to start from.</param>
    /// <returns>A <see cref="StartPosition" /> representing the given offset.</returns>
    public static StartPosition Offset(long offset) => new(Kind.Offset, offset: offset);

    /// <summary>
    ///     Start consuming from the first message published at or after the given point in time. Beware of computing
    ///     this relative to the current time — the value is re-evaluated on every process restart and will never
    ///     converge (see the type-level remarks).
    /// </summary>
    /// <param name="timestamp">The point in time to start from.</param>
    /// <returns>A <see cref="StartPosition" /> representing the given timestamp.</returns>
    public static StartPosition Timestamp(DateTime timestamp) => new(Kind.Timestamp, timestamp: timestamp);

    /// <summary>
    ///     Dispatches to exactly one of the supplied functions depending on which factory created this instance.
    ///     Transport implementations use this to translate the start position into their broker-specific consume
    ///     argument without this type knowing any broker-specific representation.
    /// </summary>
    /// <param name="first">Invoked when this instance was created via <see cref="First" />.</param>
    /// <param name="last">Invoked when this instance was created via <see cref="Last" />.</param>
    /// <param name="offset">Invoked with the offset value when this instance was created via <see cref="Offset" />.</param>
    /// <param name="timestamp">Invoked with the timestamp value when this instance was created via <see cref="Timestamp" />.</param>
    /// <typeparam name="TResult">The result type produced by all four functions.</typeparam>
    /// <returns>The value returned by the invoked function.</returns>
    public TResult Match<TResult>(Func<TResult> first, Func<TResult> last, Func<long, TResult> offset, Func<DateTime, TResult> timestamp)
        => _kind switch
        {
            Kind.First => first(),
            Kind.Last => last(),
            Kind.Offset => offset(_offset),
            Kind.Timestamp => timestamp(_timestamp),
            _ => throw new InvalidOperationException($"Unknown start position kind '{_kind}'.")
        };

    /// <summary>
    ///     Determines whether this start position equals <paramref name="other" /> — same factory kind and, where
    ///     applicable, same offset or timestamp value.
    /// </summary>
    /// <param name="other">The start position to compare with.</param>
    /// <returns><see langword="true" /> when both represent the same position.</returns>
    public bool Equals(StartPosition other) => _kind == other._kind && _offset == other._offset && _timestamp == other._timestamp;

    /// <summary>
    ///     Determines whether <paramref name="obj" /> is a <see cref="StartPosition" /> equal to this instance.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true" /> when <paramref name="obj" /> is an equal <see cref="StartPosition" />.</returns>
    public override bool Equals(object? obj) => obj is StartPosition other && Equals(other);

    /// <summary>
    ///     Returns a hash code consistent with <see cref="Equals(StartPosition)" />.
    /// </summary>
    /// <returns>The hash code for this start position.</returns>
    public override int GetHashCode() => HashCode.Combine(_kind, _offset, _timestamp);
}
