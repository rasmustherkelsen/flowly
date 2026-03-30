using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

public static class FlowlyInstrumentationConstants
{
    public const string MeterName = "Flowly";
    public const string ActivitySourceName = "Flowly";

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

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
