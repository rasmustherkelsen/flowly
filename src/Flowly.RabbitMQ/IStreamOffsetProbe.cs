namespace Flowly.RabbitMQ;

/// <summary>
///     Reads offsets off a RabbitMQ stream by attaching a short-lived consumer and watching what the broker delivers,
///     because AMQP has no call that reports a stream's tail or maps a timestamp to an offset.
/// </summary>
internal interface IStreamOffsetProbe
{
    /// <summary>
    ///     Attaches at the last chunk of <paramref name="streamName" /> and reports the offsets of the messages in it. The highest
    ///     delivered offset plus one is the offset the next appended message will not be below.
    /// </summary>
    Task<StreamProbeResult> ProbeLastChunk(string streamName, CancellationToken cancellationToken);

    /// <summary>
    ///     Attaches at <paramref name="timestamp" /> and reports the offsets of the messages the broker delivers from there. The first
    ///     delivered offset is at or before the first message published at or after <paramref name="timestamp" />, since the
    ///     broker resolves timestamps at chunk granularity.
    /// </summary>
    Task<StreamProbeResult> ProbeFromTimestamp(string streamName, DateTime timestamp, CancellationToken cancellationToken);
}

internal enum StreamProbeStatus
{
    Delivered,
    Empty,
    Inconclusive
}

internal readonly record struct StreamProbeResult(StreamProbeStatus Status, long FirstOffset, long LastOffset)
{
    public static StreamProbeResult Empty => new(StreamProbeStatus.Empty, 0, 0);

    public static StreamProbeResult Inconclusive => new(StreamProbeStatus.Inconclusive, 0, 0);

    public static StreamProbeResult Delivered(long firstOffset, long lastOffset) => new(StreamProbeStatus.Delivered, firstOffset, lastOffset);
}
