namespace Flowly;

/// <summary>
///     Options for configuring a <see cref="MessageStreamHandler{TMessage}" />. Deliberately does not inherit
///     <see cref="HandlerQueueOptions" /> — stream queues have no per-message peek-lock, no classic message TTL or
///     dead-letter queue (retention replaces TTL), and retry is an in-process loop rather than a broker requeue, so
///     lock duration, dead-letter-on-expiration, and retry fields do not apply. Retry for stream handlers is
///     configured exclusively via <see cref="RetryPolicyAttribute" /> on the handler class.
/// </summary>
public class MessageStreamHandlerOptions
{
    /// <summary>
    ///     The name of the underlying stream queue. If not set, default convention based naming will be used.
    /// </summary>
    public string? QueueName { get; set; }

    /// <summary>
    ///     The position in the stream from which consumption begins. <strong>Required</strong> — registration throws
    ///     <see cref="InvalidOperationException" /> when no start position has been set, forcing an explicit choice
    ///     between <see cref="Flowly.StartPosition.First" />, <see cref="Flowly.StartPosition.Last" />,
    ///     <see cref="Flowly.StartPosition.Offset" />, and <see cref="Flowly.StartPosition.Timestamp" />.
    ///     <para>
    ///         Re-evaluated fresh on every process restart — offsets are not persisted. Avoid values computed relative
    ///         to the current time (e.g. <c>StartPosition.Timestamp(DateTime.UtcNow - TimeSpan.FromHours(2))</c>),
    ///         which never converge across restarts.
    ///     </para>
    /// </summary>
    public StartPosition? StartPosition { get; set; }

    /// <summary>
    ///     The number of messages to accumulate before invoking the handler. If not set, defaults to 100.
    /// </summary>
    public int? MaxMessagesBeforeProcessing { get; set; }

    /// <summary>
    ///     The maximum amount of time to wait before invoking the handler with the messages accumulated so far (at
    ///     least one), even if <see cref="MaxMessagesBeforeProcessing" /> has not been reached. If not set, defaults
    ///     to 30 seconds.
    /// </summary>
    public TimeSpan? MaxWaitTime { get; set; }

    /// <summary>
    ///     The maximum age of messages retained in the stream, in seconds. Overrides the value from
    ///     <see cref="StreamRetentionAttribute" /> on the message contract. When neither is set the stream retains
    ///     messages indefinitely, bounded only by <see cref="MaxLengthBytes" />.
    /// </summary>
    public int? MaxAgeSeconds { get; set; }

    /// <summary>
    ///     The maximum total size of the stream in bytes. Overrides the value from
    ///     <see cref="StreamRetentionAttribute" /> on the message contract. When neither this nor
    ///     <see cref="MaxAgeSeconds" /> is set the stream grows unbounded — a broker disk exhaustion risk.
    /// </summary>
    public long? MaxLengthBytes { get; set; }
}
