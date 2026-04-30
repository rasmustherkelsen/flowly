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
    public const string HandlerMessagesReceived = "flowly.handler.messages.received";

    /// <summary>Counter: number of messages successfully processed by handlers.</summary>
    public const string HandlerMessagesSucceeded = "flowly.handler.messages.succeeded";

    /// <summary>Counter: number of messages that permanently failed (dead-lettered or discarded) in handlers.</summary>
    public const string HandlerMessagesFailed = "flowly.handler.messages.failed";

    /// <summary>Counter: number of messages re-published for a retry attempt by handlers.</summary>
    public const string HandlerMessagesRetried = "flowly.handler.messages.retried";

    /// <summary>Histogram: message processing duration in milliseconds for handlers.</summary>
    public const string HandlerProcessingDuration = "flowly.handler.processing.duration";

    /// <summary>Counter: number of messages successfully sent by submitters.</summary>
    public const string SubmitterMessagesSent = "flowly.submitter.messages.sent";

    /// <summary>Counter: number of send failures by submitters.</summary>
    public const string SubmitterMessagesFailed = "flowly.submitter.messages.failed";

    /// <summary>Histogram: message send duration in milliseconds for submitters.</summary>
    public const string SubmitterSendDuration = "flowly.submitter.send.duration";

    /// <summary>Gauge: current number of pending dead-lettered messages across all tracked queues.</summary>
    public const string DeadLettersPending = "flowly.deadletters.pending";

    /// <summary>Gauge: current number of jobs in the <c>Failed</c> state.</summary>
    public const string JobsFailed = "flowly.jobs.failed";

    /// <summary>Gauge: current number of jobs in the <c>Started</c> state.</summary>
    public const string JobsRunning = "flowly.jobs.running";

    /// <summary>Counter: number of events received by event handlers.</summary>
    public const string EventHandlerMessagesReceived = "flowly.event.handler.received";

    /// <summary>Counter: number of events successfully processed by event handlers.</summary>
    public const string EventHandlerMessagesSucceeded = "flowly.event.handler.succeeded";

    /// <summary>Counter: number of events that permanently failed in event handlers.</summary>
    public const string EventHandlerMessagesFailed = "flowly.event.handler.failed";

    /// <summary>Counter: number of events re-published for a retry attempt by event handlers.</summary>
    public const string EventHandlerMessagesRetried = "flowly.event.handler.retried";

    /// <summary>Histogram: event processing duration in milliseconds for event handlers.</summary>
    public const string EventHandlerProcessingDuration = "flowly.event.handler.processing.duration";

    /// <summary>Counter: number of events successfully raised by event publishers.</summary>
    public const string EventPublisherEventsRaised = "flowly.event.publisher.raised";

    /// <summary>Counter: number of event publish failures.</summary>
    public const string EventPublisherEventsFailed = "flowly.event.publisher.failed";

    /// <summary>Histogram: event publish duration in milliseconds.</summary>
    public const string EventPublisherRaiseDuration = "flowly.event.publisher.raise.duration";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
