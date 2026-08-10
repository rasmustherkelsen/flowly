namespace Flowly;

/// <summary>
///     The position in a message stream from which a <see cref="MessageStreamHandler{TMessage}" /> begins consuming.
///     Created via the static factory methods <see cref="First" />, <see cref="Last" />, <see cref="Offset" />, and
///     <see cref="Timestamp" /> — there is no public constructor and no default value; every stream handler must pick
///     a start position explicitly in <see cref="MessageStreamHandler{TMessage}.Configure" />.
///     <para>
///         <strong>The start position is not persisted across restarts.</strong> It is re-evaluated fresh on every
///         process boot — Flowly does not checkpoint stream offsets. A value computed relative to the current time
///         (e.g. <c>StartPosition.Timestamp(DateTime.UtcNow - TimeSpan.FromHours(2))</c>) is recomputed on every
///         restart and will never converge on a fixed point in the stream. Reprocessing already-handled messages
///         (with <see cref="First" />, <see cref="Offset" />, or <see cref="Timestamp" />) or missing messages
///         published while the process was down (with <see cref="Last" />) is an explicit operator tradeoff.
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
    ///     Messages published while the process was down are not delivered after a restart.
    /// </summary>
    /// <returns>A <see cref="StartPosition" /> representing the tail of the stream.</returns>
    public static StartPosition Last() => new(Kind.Last);

    /// <summary>
    ///     Start consuming from a specific numeric offset in the stream. Offsets below the retention floor are clamped
    ///     to the first retained message by the broker.
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
