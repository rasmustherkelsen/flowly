using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

/// <summary>
///     Provides OpenTelemetry tracing and metrics instrumentation for message handlers. The framework resolves this
///     interface from the DI container and calls its methods at defined points in the message processing lifecycle.
///     Two implementations are registered depending on <see cref="FlowlyOptions.EnableTelemetry" />:
///     <c>HandlerInstrumentation</c> (active) and <c>NullHandlerInstrumentation</c> (no-op).
/// </summary>
public interface IHandlerInstrumentation
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
    ///     Increments the <c>flowly.handler.messages.received</c> counter. Called immediately when a message arrives.
    /// </summary>
    /// <param name="handlerName">The name of the handler class.</param>
    /// <param name="queueName">The queue the message was received from.</param>
    /// <param name="count">The number of messages to record (default <c>1</c>).</param>
    void RecordReceived(string handlerName, string queueName, long count = 1);

    /// <summary>
    ///     Increments the <c>flowly.handler.messages.succeeded</c> counter and records the processing duration.
    ///     Called after the handler completes without throwing.
    /// </summary>
    /// <param name="handlerName">The name of the handler class.</param>
    /// <param name="queueName">The queue the message was received from.</param>
    /// <param name="durationMs">The elapsed processing time in milliseconds.</param>
    /// <param name="count">The number of messages to record (default <c>1</c>).</param>
    void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1);

    /// <summary>
    ///     Increments the <c>flowly.handler.messages.failed</c> counter. Called when all retries are exhausted or when
    ///     a poison message is dead-lettered.
    /// </summary>
    /// <param name="handlerName">The name of the handler class.</param>
    /// <param name="queueName">The queue the message was received from.</param>
    /// <param name="count">The number of messages to record (default <c>1</c>).</param>
    void RecordFailed(string handlerName, string queueName, long count = 1);

    /// <summary>
    ///     Increments the <c>flowly.handler.messages.retried</c> counter. Called when a message is re-published for a
    ///     retry attempt.
    /// </summary>
    /// <param name="handlerName">The name of the handler class.</param>
    /// <param name="queueName">The queue the message was received from.</param>
    void RecordRetried(string handlerName, string queueName);
}
