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
public record MessageProperties(
    string MessageId,
    string CorrelationId,
    string? SessionId = null,
    int RetryCount = 0,
    DateTimeOffset? ScheduledEnqueueTime = null,
    string? Traceparent = null,
    string? Tracestate = null)
{
    /// <summary>
    ///     A default empty <see cref="MessageProperties" /> instance with empty strings for required fields. Used as a
    ///     placeholder in contexts where real properties are not available.
    /// </summary>
    public static readonly MessageProperties Empty = new(string.Empty, string.Empty, string.Empty);
}