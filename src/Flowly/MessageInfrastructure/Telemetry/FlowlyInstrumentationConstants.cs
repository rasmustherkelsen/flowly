using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

/// <summary>
///     String constants for OpenTelemetry meter names, activity source names, W3C messaging semantic convention
///     attribute names, and Flowly metric instrument names. Use these constants when configuring
///     <c>MeterProviderBuilder</c> or <c>TracerProviderBuilder</c> in <c>Flowly.OpenTelemetry</c>, or when querying
///     metrics by name.
/// </summary>
public static class FlowlyInstrumentationConstants
{
    /// <summary>The OTel meter name used for all Flowly metrics (<c>"Flowly"</c>).</summary>
    public const string MeterName = "Flowly";

    /// <summary>The OTel activity source name used for all Flowly traces (<c>"Flowly"</c>).</summary>
    public const string ActivitySourceName = "Flowly";

    /// <summary>OTel semantic convention: the messaging system (e.g. <c>azure-service-bus</c>).</summary>
    public const string MessagingSystem = "messaging.system";

    /// <summary>OTel semantic convention: the destination queue or topic name.</summary>
    public const string MessagingDestinationName = "messaging.destination.name";

    /// <summary>OTel semantic convention: the operation type (e.g. <c>publish</c>, <c>receive</c>).</summary>
    public const string MessagingOperationType = "messaging.operation.type";

    /// <summary>OTel semantic convention: the broker-assigned message identifier.</summary>
    public const string MessagingMessageId = "messaging.message.id";

    /// <summary>OTel semantic convention: the correlation / conversation identifier.</summary>
    public const string MessagingMessageConversationId = "messaging.message.conversation_id";

    /// <summary>Counter: number of messages received by handlers.</summary>
    public const string HandlerMessagesReceived = "flowly.message.handler.received";

    /// <summary>Counter: number of messages successfully processed by handlers.</summary>
    public const string HandlerMessagesSucceeded = "flowly.message.handler.succeeded";

    /// <summary>Counter: number of messages that permanently failed (dead-lettered or discarded) in handlers.</summary>
    public const string HandlerMessagesFailed = "flowly.message.handler.failed";

    /// <summary>Counter: number of messages re-published for a retry attempt by handlers.</summary>
    public const string HandlerMessagesRetried = "flowly.message.handler.retried";

    /// <summary>Histogram: message processing duration in milliseconds for handlers.</summary>
    public const string HandlerProcessingDuration = "flowly.message.handler.duration";

    /// <summary>Counter: number of messages successfully sent by submitters.</summary>
    public const string SubmitterMessagesSent = "flowly.message.submitter.sent";

    /// <summary>Counter: number of send failures by submitters.</summary>
    public const string SubmitterMessagesFailed = "flowly.message.submitter.failed";

    /// <summary>Histogram: message send duration in milliseconds for submitters.</summary>
    public const string SubmitterSendDuration = "flowly.message.submitter.duration";

    /// <summary>Gauge: current number of pending dead-lettered messages across all tracked queues.</summary>
    public const string DeadLettersPending = "flowly.deadletter.pending";

    /// <summary>Gauge: current number of jobs in the <c>Failed</c> state.</summary>
    public const string JobsFailed = "flowly.job.failed";

    /// <summary>Gauge: current number of jobs in the <c>Started</c> state.</summary>
    public const string JobsRunning = "flowly.job.running";

    /// <summary>Counter: number of events received by event handlers.</summary>
    public const string EventHandlerMessagesReceived = "flowly.event.handler.received";

    /// <summary>Counter: number of events successfully processed by event handlers.</summary>
    public const string EventHandlerMessagesSucceeded = "flowly.event.handler.succeeded";

    /// <summary>Counter: number of events that permanently failed in event handlers.</summary>
    public const string EventHandlerMessagesFailed = "flowly.event.handler.failed";

    /// <summary>Counter: number of events re-published for a retry attempt by event handlers.</summary>
    public const string EventHandlerMessagesRetried = "flowly.event.handler.retried";

    /// <summary>Histogram: event processing duration in milliseconds for event handlers.</summary>
    public const string EventHandlerProcessingDuration = "flowly.event.handler.duration";

    /// <summary>Counter: number of events successfully raised by event publishers.</summary>
    public const string EventPublisherEventsRaised = "flowly.event.publisher.raised";

    /// <summary>Counter: number of event publish failures.</summary>
    public const string EventPublisherEventsFailed = "flowly.event.publisher.failed";

    /// <summary>Histogram: event publish duration in milliseconds.</summary>
    public const string EventPublisherRaiseDuration = "flowly.event.publisher.duration";

    /// <summary>Counter: number of calls that completed successfully (response received before timeout).</summary>
    public const string CallerCallsSucceeded = "flowly.call.succeeded";

    /// <summary>Counter: number of calls that failed (timed out or threw).</summary>
    public const string CallerCallsFailed = "flowly.call.failed";

    /// <summary>Histogram: end-to-end call round-trip duration in milliseconds (from Call() to response).</summary>
    public const string CallerCallDuration = "flowly.call.duration";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    ///     Resolves the <see cref="ActivityContext" /> to use as the parent for a producer (send/publish) span.
    /// </summary>
    /// <remarks>
    ///     When <c>default(ActivityContext)</c> is passed to <c>ActivitySource.StartActivity</c>, the .NET
    ///     runtime falls back to <c>Activity.Current.Context</c> before invoking OTel listeners. If
    ///     <c>Activity.Current</c> is an unsampled activity — e.g. the ASP.NET Core request activity when
    ///     <c>AddAspNetCoreInstrumentation()</c> is not registered — <c>ParentBasedSampler</c> propagates the
    ///     "not sampled" decision and drops the Flowly span. Explicitly supplying a fresh context with a new
    ///     <see cref="ActivityTraceId" /> bypasses the runtime's substitution and lets the sampler make an
    ///     independent decision.
    /// </remarks>
    internal static ActivityContext ResolveProducerParentContext()
    {
        var current = Activity.Current;

        if (current == null)
            return default;

        if (current.Recorded)
            return current.Context;

        return new ActivityContext(ActivityTraceId.CreateRandom(), default, ActivityTraceFlags.Recorded);
    }
}
