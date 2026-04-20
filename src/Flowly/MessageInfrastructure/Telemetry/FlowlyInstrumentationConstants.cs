using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

public static class FlowlyInstrumentationConstants
{
    public const string MeterName = "Flowly";
    public const string ActivitySourceName = "Flowly";

    // OTel messaging semantic convention attribute names
    public const string MessagingSystem = "messaging.system";
    public const string MessagingDestinationName = "messaging.destination.name";
    public const string MessagingOperationType = "messaging.operation.type";
    public const string MessagingMessageId = "messaging.message.id";
    public const string MessagingMessageConversationId = "messaging.message.conversation_id";

    public const string HandlerMessagesReceived = "flowly.handler.messages.received";
    public const string HandlerMessagesSucceeded = "flowly.handler.messages.succeeded";
    public const string HandlerMessagesFailed = "flowly.handler.messages.failed";
    public const string HandlerMessagesRetried = "flowly.handler.messages.retried";
    public const string HandlerProcessingDuration = "flowly.handler.processing.duration";

    public const string SubmitterMessagesSent = "flowly.submitter.messages.sent";
    public const string SubmitterMessagesFailed = "flowly.submitter.messages.failed";
    public const string SubmitterSendDuration = "flowly.submitter.send.duration";

    public const string DeadLettersPending = "flowly.deadletters.pending";

    public const string JobsFailed = "flowly.jobs.failed";
    public const string JobsRunning = "flowly.jobs.running";

    public const string EventHandlerMessagesReceived = "flowly.event.handler.received";
    public const string EventHandlerMessagesSucceeded = "flowly.event.handler.succeeded";
    public const string EventHandlerMessagesFailed = "flowly.event.handler.failed";
    public const string EventHandlerMessagesRetried = "flowly.event.handler.retried";
    public const string EventHandlerProcessingDuration = "flowly.event.handler.processing.duration";

    public const string EventPublisherEventsRaised = "flowly.event.publisher.raised";
    public const string EventPublisherEventsFailed = "flowly.event.publisher.failed";
    public const string EventPublisherRaiseDuration = "flowly.event.publisher.raise.duration";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
