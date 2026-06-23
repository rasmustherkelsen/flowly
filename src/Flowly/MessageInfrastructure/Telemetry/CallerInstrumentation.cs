using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Flowly.MessageInfrastructure.Telemetry;

/// <summary>
///     Active implementation of <see cref="ICallerInstrumentation" /> that emits OTel metrics via
///     <see cref="FlowlyInstrumentationConstants.MeterName" />. Registered when
///     <see cref="FlowlyOptions.EnableTelemetry" /> is <see langword="true" />.
/// </summary>
internal sealed class CallerInstrumentation : ICallerInstrumentation, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _succeeded;
    private readonly Counter<long> _failed;
    private readonly Histogram<double> _duration;

    /// <summary>Initialises the OTel meter and creates all metric instruments.</summary>
    public CallerInstrumentation()
    {
        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _succeeded = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.CallerCallsSucceeded);
        _failed = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.CallerCallsFailed);
        _duration = _meter.CreateHistogram<double>(FlowlyInstrumentationConstants.CallerCallDuration, "ms");
    }

    /// <inheritdoc />
    public Activity? StartCalling(string callQueueName, string messagingSystem, string messageId, string correlationId)
        => FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.call {callQueueName}",
            ActivityKind.Client,
            FlowlyInstrumentationConstants.ResolveProducerParentContext(),
            [
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingSystem, messagingSystem),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingDestinationName, callQueueName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageId, messageId),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageConversationId, correlationId),
            ]);

    /// <inheritdoc />
    public void RecordSucceeded(string callQueueName, double durationMs)
    {
        Activity.Current?.SetTag("outcome", "success");

        var tags = new TagList { { FlowlyInstrumentationConstants.MessagingDestinationName, callQueueName } };

        _succeeded.Add(1, tags);
        _duration.Record(durationMs, tags);
    }

    /// <inheritdoc />
    public void RecordFailed(string callQueueName)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error);
        Activity.Current?.SetTag("outcome", "failed");

        _failed.Add(1, new TagList { { FlowlyInstrumentationConstants.MessagingDestinationName, callQueueName } });
    }

    /// <inheritdoc />
    public Activity? StartReceivingResponse(string replyQueueName, string messagingSystem, ActivityContext parentContext, string messageId, string correlationId)
        => FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.call.response {replyQueueName}",
            ActivityKind.Consumer,
            parentContext,
            [
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingSystem, messagingSystem),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingDestinationName, replyQueueName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingOperationType, "receive"),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageId, messageId),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageConversationId, correlationId),
            ]);

    /// <summary>Disposes the underlying OTel <see cref="Meter" />.</summary>
    public void Dispose() => _meter.Dispose();
}
