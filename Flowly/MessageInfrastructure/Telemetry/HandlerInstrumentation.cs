using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Flowly.MessageInfrastructure.Telemetry;

public sealed class HandlerInstrumentation : IDisposable
{
    private readonly bool _enabled;
    private readonly Meter? _meter;
    private readonly Counter<long>? _received;
    private readonly Counter<long>? _succeeded;
    private readonly Counter<long>? _failed;
    private readonly Counter<long>? _retried;
    private readonly Histogram<double>? _duration;

    public HandlerInstrumentation(bool enabled)
    {
        _enabled = enabled;
        if (!enabled) return;

        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _received = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.HandlerMessagesReceived);
        _succeeded = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.HandlerMessagesSucceeded);
        _failed = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.HandlerMessagesFailed);
        _retried = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.HandlerMessagesRetried);
        _duration = _meter.CreateHistogram<double>(FlowlyInstrumentationConstants.HandlerProcessingDuration, "ms");
    }

    public Activity? StartHandling(string handlerName, string queueName)
    {
        if (!_enabled) return null;

        return FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.handle {queueName}",
            ActivityKind.Consumer,
            default(ActivityContext),
            [new KeyValuePair<string, object?>("handler", handlerName), new KeyValuePair<string, object?>("queue", queueName)]);
    }

    internal bool IsEnabled => _meter != null;

    public void RecordReceived(string handlerName, string queueName, long count = 1)
        => _received?.Add(count, new TagList { { "handler", handlerName }, { "queue", queueName } });

    public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1)
    {
        Activity.Current?.SetTag("outcome", "success");
        var tags = new TagList { { "handler", handlerName }, { "queue", queueName } };
        _succeeded?.Add(count, tags);
        _duration?.Record(durationMs, tags);
    }

    public void RecordFailed(string handlerName, string queueName, long count = 1)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error);
        Activity.Current?.SetTag("outcome", "failed");
        _failed?.Add(count, new TagList { { "handler", handlerName }, { "queue", queueName } });
    }

    public void RecordRetried(string handlerName, string queueName)
    {
        Activity.Current?.SetTag("outcome", "retry");
        _retried?.Add(1, new TagList { { "handler", handlerName }, { "queue", queueName } });
    }

    public void Dispose() => _meter?.Dispose();
}
