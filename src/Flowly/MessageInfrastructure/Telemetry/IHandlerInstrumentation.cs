using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

/// <summary>
///     Provides OpenTelemetry tracing and metrics instrumentation for message handlers. The framework resolves this
///     interface from the DI container and calls its methods at defined points in the message processing lifecycle.
///     Two implementations are registered depending on <see cref="FlowlyOptions.EnableTelemetry" />:
///     <c>HandlerInstrumentation</c> (active) and <c>NullHandlerInstrumentation</c> (no-op).
/// </summary>
internal interface IHandlerInstrumentation
{
    /// <summary>
    ///     <see langword="true" /> when telemetry is active; <see langword="false" /> for the no-op implementation.
    ///     Transport providers and custom strategies can check this before performing expensive context extraction.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Starts an <see cref="Activity" /> for processing a single message, propagating the W3C trace context from
    ///     <paramref name="messageProperties" /> if present. Called by the message processing background service before
    ///     the handler is invoked.
    /// </summary>
    /// <param name="handlerName">The name of the handler class, used as the span name.</param>
    /// <param name="queueName">The queue the message was received from.</param>
    /// <param name="messagingSystem">The transport identifier (e.g. <c>"azure-service-bus"</c>).</param>
    /// <param name="messageProperties">Message metadata containing trace propagation headers.</param>
    /// <param name="parentContext">The parent <see cref="ActivityContext" /> extracted from the message headers.</param>
    /// <returns>The started <see cref="Activity" />, or <see langword="null" /> if telemetry is disabled.</returns>
    Activity? StartHandling(
        string handlerName,
        string queueName,
        string messagingSystem,
        MessageProperties messageProperties,
        ActivityContext parentContext = default);

    /// <summary>
    ///     Starts an <see cref="Activity" /> for processing a batch of messages. Because the batch may contain messages
    ///     from multiple independent producers, trace context is expressed as <see cref="ActivityLink" />s rather than a
    ///     single parent, allowing monitoring tools such as Jaeger to navigate upstream from the batch consumer span to
    ///     each individual producer span.
    /// </summary>
    /// <param name="handlerName">The name of the handler class, used as the span tag.</param>
    /// <param name="queueName">The queue the messages were received from.</param>
    /// <param name="messagingSystem">The transport identifier (e.g. <c>"azure-service-bus"</c>).</param>
    /// <param name="messageProperties">
    ///     Metadata from the first message in the batch, used for message ID and correlation ID span tags.
    /// </param>
    /// <param name="links">
    ///     One <see cref="ActivityLink" /> per producer span extracted from the batch messages' trace headers.
    /// </param>
    /// <returns>The started <see cref="Activity" />, or <see langword="null" /> if telemetry is disabled.</returns>
    Activity? StartHandling(
        string handlerName,
        string queueName,
        string messagingSystem,
        MessageProperties messageProperties,
        IEnumerable<ActivityLink> links);

    /// <summary>
    ///     Increments the <c>flowly.message.handler.received</c> counter. Called immediately when a message arrives.
    /// </summary>
    /// <param name="handlerName">The name of the handler class.</param>
    /// <param name="queueName">The queue the message was received from.</param>
    /// <param name="count">The number of messages to record (default <c>1</c>).</param>
    void RecordReceived(string handlerName, string queueName, long count = 1);

    /// <summary>
    ///     Increments the <c>flowly.message.handler.succeeded</c> counter and records the processing duration.
    ///     Called after the handler completes without throwing.
    /// </summary>
    /// <param name="handlerName">The name of the handler class.</param>
    /// <param name="queueName">The queue the message was received from.</param>
    /// <param name="durationMs">The elapsed processing time in milliseconds.</param>
    /// <param name="count">The number of messages to record (default <c>1</c>).</param>
    void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1);

    /// <summary>
    ///     Increments the <c>flowly.message.handler.failed</c> counter. Called when all retries are exhausted or when
    ///     a poison message is dead-lettered.
    /// </summary>
    /// <param name="handlerName">The name of the handler class.</param>
    /// <param name="queueName">The queue the message was received from.</param>
    /// <param name="count">The number of messages to record (default <c>1</c>).</param>
    void RecordFailed(string handlerName, string queueName, long count = 1);

    /// <summary>
    ///     Increments the <c>flowly.message.handler.retried</c> counter. Called when a message is re-published for a
    ///     retry attempt.
    /// </summary>
    /// <param name="handlerName">The name of the handler class.</param>
    /// <param name="queueName">The queue the message was received from.</param>
    /// <param name="count">The number of messages to record (default <c>1</c>).</param>
    void RecordRetried(string handlerName, string queueName, long count = 1);

    /// <summary>
    ///     Increments the <c>flowly.message.handler.halted</c> counter, sets the current activity status to
    ///     <see cref="ActivityStatusCode.Error" />, and records an activity event carrying the failure reason. Called
    ///     when a <see cref="MessageStreamHandler{TMessage}" /> exhausts its in-process retries and stops consuming its
    ///     stream queue entirely rather than skipping the failed batch — a critical signal requiring operator
    ///     intervention.
    /// </summary>
    /// <param name="handlerName">The name of the handler class.</param>
    /// <param name="queueName">The stream queue the handler stopped consuming.</param>
    /// <param name="reason">The failure reason that caused the halt.</param>
    void RecordHalted(string handlerName, string queueName, string reason);

    /// <summary>
    ///     Starts a <see cref="ActivityKind.Producer" /> span named <c>flowly.call.reply {callQueueName}</c> for the
    ///     act of sending a response back to the caller. The activity becomes <c>Activity.Current</c> so the transport
    ///     can inject its <c>traceparent</c> into the outgoing response message, linking it as a child of the handler
    ///     consumer span.
    /// </summary>
    /// <param name="callQueueName">The incoming call queue name — used as the span name suffix and metric tag.</param>
    /// <param name="replyQueueName">The reply-to queue address — used as the <c>messaging.destination.name</c> tag.</param>
    /// <param name="messagingSystem">The transport system name (e.g. <c>azure-service-bus</c>).</param>
    /// <param name="messageId">Unique identifier for the response message.</param>
    /// <param name="correlationId">Correlation ID echoed from the original call message.</param>
    /// <returns>The started <see cref="Activity" />, or <see langword="null" /> when not sampled.</returns>
    Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId);

    /// <summary>
    ///     Increments the <c>flowly.call.handler.replied</c> counter and records the response send duration.
    ///     Called after the response message is successfully delivered to the reply queue.
    /// </summary>
    /// <param name="callQueueName">The call queue name used as a metric tag.</param>
    /// <param name="durationMs">Elapsed time in milliseconds from response send start to delivery.</param>
    void RecordResponseSent(string callQueueName, double durationMs);

    /// <summary>
    ///     Increments the <c>flowly.call.handler.reply.failed</c> counter and sets the current activity status to
    ///     <see cref="ActivityStatusCode.Error" />. Called when sending the response message throws.
    /// </summary>
    /// <param name="callQueueName">The call queue name used as a metric tag.</param>
    void RecordResponseFailed(string callQueueName);
}
