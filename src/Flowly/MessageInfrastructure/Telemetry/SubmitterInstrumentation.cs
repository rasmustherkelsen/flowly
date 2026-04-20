using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Flowly.MessageInfrastructure.Telemetry;

public sealed class SubmitterInstrumentation : ISubmitterInstrumentation, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _sent;
    private readonly Counter<long> _failed;
    private readonly Histogram<double> _duration;

    public SubmitterInstrumentation()
    {
        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _sent = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.SubmitterMessagesSent);
        _failed = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.SubmitterMessagesFailed);
        _duration = _meter.CreateHistogram<double>(FlowlyInstrumentationConstants.SubmitterSendDuration, "ms");
    }

    public bool IsEnabled => true;

    public Activity? StartSending(string queueName, string messagingSystem, string messageId)
        => FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.send {queueName}",
            ActivityKind.Producer,
            default(ActivityContext),
            [
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingSystem, messagingSystem),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingDestinationName, queueName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingOperationType, "publish"),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageId, messageId),
            ]);

    public void RecordSent(string queueName, double durationMs)
    {
        Activity.Current?.SetTag("outcome", "success");

        var tags = new TagList { { FlowlyInstrumentationConstants.MessagingDestinationName, queueName } };

        _sent.Add(1, tags);
        _duration.Record(durationMs, tags);
    }

    public void RecordFailed(string queueName)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error);
        Activity.Current?.SetTag("outcome", "failed");

        _failed.Add(1, new TagList { { FlowlyInstrumentationConstants.MessagingDestinationName, queueName } });
    }

    public void Dispose() => _meter.Dispose();
}
