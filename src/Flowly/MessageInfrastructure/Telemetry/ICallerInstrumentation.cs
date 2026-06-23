using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

/// <summary>
///     Provides OpenTelemetry traces and metrics instrumentation for <see cref="IMessageCaller" /> call operations.
///     Measures the end-to-end round-trip duration from <c>Call()</c> invocation to response receipt.
/// </summary>
internal interface ICallerInstrumentation
{
    /// <summary>
    ///     Starts a <see cref="ActivityKind.Client" /> span named <c>flowly.call {callQueueName}</c> covering the
    ///     full RPC round-trip. The activity becomes <c>Activity.Current</c> so the transport can inject its
    ///     <c>traceparent</c> into the outgoing call message, linking the handler's consumer span as a child.
    /// </summary>
    /// <param name="callQueueName">The call queue name — used as the span name suffix and a tag.</param>
    /// <param name="messagingSystem">The transport system name (e.g. <c>azure-service-bus</c>).</param>
    /// <param name="messageId">Unique identifier for the call message.</param>
    /// <param name="correlationId">Correlation ID used to match request to response.</param>
    /// <returns>The started <see cref="Activity" />, or <see langword="null" /> when not sampled.</returns>
    Activity? StartCalling(string callQueueName, string messagingSystem, string messageId, string correlationId);

    /// <summary>
    ///     Increments the <c>flowly.call.succeeded</c> counter and records the end-to-end round-trip duration.
    ///     Called when a response is received before the timeout.
    /// </summary>
    /// <param name="callQueueName">The call queue name used as a metric tag.</param>
    /// <param name="durationMs">Total elapsed time in milliseconds from call start to response.</param>
    void RecordSucceeded(string callQueueName, double durationMs);

    /// <summary>
    ///     Increments the <c>flowly.call.failed</c> counter. Called when the call times out or throws.
    /// </summary>
    /// <param name="callQueueName">The call queue name used as a metric tag.</param>
    void RecordFailed(string callQueueName);

    /// <summary>
    ///     Starts a <see cref="ActivityKind.Consumer" /> span named <c>flowly.call.response {replyQueueName}</c>
    ///     for the act of receiving a call response on the reply queue. The <paramref name="parentContext" /> is
    ///     parsed from the response message's <c>Traceparent</c> property, linking this span as a child of the
    ///     handler's <c>flowly.call.reply</c> producer span.
    /// </summary>
    /// <param name="replyQueueName">The reply queue name — used as the span name suffix and a tag.</param>
    /// <param name="messagingSystem">The transport system name (e.g. <c>azure-service-bus</c>).</param>
    /// <param name="parentContext">
    ///     The remote <see cref="ActivityContext" /> extracted from the response message's <c>Traceparent</c>
    ///     header. Use <c>default</c> when no traceparent is present.
    /// </param>
    /// <param name="messageId">The unique identifier of the response message.</param>
    /// <param name="correlationId">The correlation ID matching this response to the original call.</param>
    /// <returns>The started <see cref="Activity" />, or <see langword="null" /> when not sampled.</returns>
    Activity? StartReceivingResponse(string replyQueueName, string messagingSystem, ActivityContext parentContext, string messageId, string correlationId);
}
