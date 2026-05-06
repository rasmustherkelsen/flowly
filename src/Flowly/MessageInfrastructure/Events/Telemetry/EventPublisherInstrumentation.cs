using System.Diagnostics;
using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.MessageInfrastructure.Events.Telemetry;

internal sealed class EventPublisherInstrumentation : IEventPublisherInstrumentation, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _raised;
    private readonly Counter<long> _failed;
    private readonly Histogram<double> _duration;

    public EventPublisherInstrumentation()
    {
        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _raised = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.EventPublisherEventsRaised);
        _failed = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.EventPublisherEventsFailed);
        _duration = _meter.CreateHistogram<double>(FlowlyInstrumentationConstants.EventPublisherRaiseDuration, "ms");
    }

    public bool IsEnabled => true;

    public Activity? StartRaising(string topicName, string messagingSystem, string messageId)
        => FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.event.raise {topicName}",
            ActivityKind.Producer,
            default(ActivityContext),
            [
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingSystem, messagingSystem),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingDestinationName, topicName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingOperationType, "publish"),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageId, messageId),
            ]);

    public void RecordRaised(string topicName, double durationMs)
    {
        Activity.Current?.SetTag("outcome", "success");

        var tags = new TagList { { FlowlyInstrumentationConstants.MessagingDestinationName, topicName } };

        _raised.Add(1, tags);
        _duration.Record(durationMs, tags);
    }

    public void RecordFailed(string topicName)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error);
        Activity.Current?.SetTag("outcome", "failed");

        _failed.Add(1, new TagList { { FlowlyInstrumentationConstants.MessagingDestinationName, topicName } });
    }

    public void Dispose() => _meter.Dispose();
}
