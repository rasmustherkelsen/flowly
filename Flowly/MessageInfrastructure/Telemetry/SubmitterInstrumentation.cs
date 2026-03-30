using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Flowly.MessageInfrastructure.Telemetry;

public sealed class SubmitterInstrumentation : IDisposable
{
    private readonly bool _enabled;
    private readonly Meter? _meter;
    private readonly Counter<long>? _sent;
    private readonly Counter<long>? _failed;
    private readonly Histogram<double>? _duration;

    public SubmitterInstrumentation(bool enabled)
    {
        _enabled = enabled;
        if (!enabled) return;

        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _sent = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.SubmitterMessagesSent);
        _failed = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.SubmitterMessagesFailed);
        _duration = _meter.CreateHistogram<double>(FlowlyInstrumentationConstants.SubmitterSendDuration, "ms");
    }

    internal bool IsEnabled => _meter != null;

    public Activity? StartSending(string queueName)
    {
        if (!_enabled) return null;

        return FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.send {queueName}",
            ActivityKind.Producer,
            default(ActivityContext),
            [new KeyValuePair<string, object?>("queue", queueName)]);
    }

    public void RecordSent(string queueName, double durationMs)
    {
        Activity.Current?.SetTag("outcome", "success");
        var tags = new TagList { { "queue", queueName } };
        _sent?.Add(1, tags);
        _duration?.Record(durationMs, tags);
    }

    public void RecordFailed(string queueName)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error);
        Activity.Current?.SetTag("outcome", "failed");
        _failed?.Add(1, new TagList { { "queue", queueName } });
    }

    public void Dispose() => _meter?.Dispose();
}
