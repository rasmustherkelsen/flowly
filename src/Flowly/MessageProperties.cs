using Flowly.Transport;

namespace Flowly;

/// <summary>
///     Carrier for transport-level message metadata that accompanies every message through the Flowly pipeline.
///     Populated from broker application properties when a message is received and forwarded to
///     <see cref="IReceivedMessage{TMessage}.Properties" />, and constructed by the framework when sending.
/// </summary>
/// <param name="MessageId">The unique identifier assigned to this message by the broker or the sender.</param>
/// <param name="CorrelationId">An optional correlation identifier used to associate related messages.</param>
/// <param name="SessionId">
///     The session identifier for session-based queues. Required for execution-lane (recurring job) queues.
/// </param>
/// <param name="RetryCount">
///     The current retry attempt number. Stored in the <c>flowly-retry-count</c> application property and incremented
///     by the framework on each re-publish.
/// </param>
/// <param name="ScheduledEnqueueTime">
///     When set, the broker will not deliver the message before this UTC time. Used by the retry delay mechanism.
/// </param>
/// <param name="Traceparent">
///     The W3C traceparent header value, propagated to enable distributed tracing across message boundaries.
/// </param>
/// <param name="Tracestate">
///     The W3C tracestate header value, propagated alongside <paramref name="Traceparent" />.
/// </param>
/// <param name="ReplyTo">
///     The name of the reply queue to which the call handler should send its response. Set by the Flowly
///     infrastructure when sending a call message via <see cref="IMessageCaller" />; read by
///     <c>CallMessageHandlingStrategy</c> to route the response back to the originating caller.
/// </param>
/// <param name="StreamOffset">
///     The numeric offset of this message within its stream. Only populated for messages delivered by a
///     stream-capable transport (<see langword="null" /> otherwise). Used internally to persist a
///     <see cref="MessageStreamCheckpoint{TMessage}" /> position after a batch is successfully processed.
/// </param>
/// <param name="PartitionKey">
///     The partition-routing key supplied to <see cref="IMessageRecorder.Record{TMessage}" /> when recording onto a
///     partitioned stream. Used only at send time to select a partition (via a transport-specific hash) — not
///     retained on the delivered message. <see langword="null" /> selects a partition via round-robin.
/// </param>
public record MessageProperties(
    string MessageId,
    string CorrelationId,
    string? SessionId = null,
    int RetryCount = 0,
    DateTimeOffset? ScheduledEnqueueTime = null,
    string? Traceparent = null,
    string? Tracestate = null,
    string? ReplyTo = null,
    long? StreamOffset = null,
    string? PartitionKey = null)
{
    /// <summary>
    ///     A default empty <see cref="MessageProperties" /> instance with empty strings for required fields. Used as a
    ///     placeholder in contexts where real properties are not available.
    /// </summary>
    public static readonly MessageProperties Empty = new(string.Empty, string.Empty, string.Empty);
}