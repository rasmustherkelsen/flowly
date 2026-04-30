using System.Diagnostics;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Telemetry;

/// <summary>
///     Provides OpenTelemetry tracing and metrics instrumentation for message submitters. The framework resolves this
///     interface from the DI container and calls its methods at defined points in the message send lifecycle.
///     Two implementations are registered depending on <see cref="FlowlyOptions.EnableTelemetry" />:
///     <c>SubmitterInstrumentation</c> (active) and <c>NullSubmitterInstrumentation</c> (no-op).
/// </summary>
public interface ISubmitterInstrumentation
{
    /// <summary>
    ///     <see langword="true" /> when telemetry is active; <see langword="false" /> for the no-op implementation.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Starts an <see cref="Activity" /> for a send operation and injects W3C trace context into the outgoing
    ///     message properties. Called by the message sender before invoking
    ///     <see cref="IMessageBusSender.SendMessage{TMessage}" />.
    /// </summary>
    /// <param name="queueName">The target queue or topic name.</param>
    /// <param name="messagingSystem">The transport identifier (e.g. <c>"azure-service-bus"</c>).</param>
    /// <param name="messageId">The unique message identifier to attach to the span.</param>
    /// <returns>The started <see cref="Activity" />, or <see langword="null" /> if telemetry is disabled.</returns>
    Activity? StartSending(string queueName, string messagingSystem, string messageId);

    /// <summary>
    ///     Increments the <c>flowly.submitter.messages.sent</c> counter and records send duration. Called after the
    ///     message is successfully handed off to the broker.
    /// </summary>
    /// <param name="queueName">The target queue or topic name.</param>
    /// <param name="durationMs">The elapsed send time in milliseconds.</param>
    void RecordSent(string queueName, double durationMs);

    /// <summary>
    ///     Increments the <c>flowly.submitter.messages.failed</c> counter. Called when the send operation throws.
    /// </summary>
    /// <param name="queueName">The target queue or topic name.</param>
    void RecordFailed(string queueName);
}