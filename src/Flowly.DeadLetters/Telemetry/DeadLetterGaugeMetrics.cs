using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.DeadLetters.Telemetry;

internal sealed class DeadLetterGaugeMetrics : IDisposable
{
    private readonly Meter? _meter;
    private long _pendingCount;

    public DeadLetterGaugeMetrics(FlowlyOptions options)
    {
        if (!options.EnableTelemetry) return;

        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _meter.CreateObservableGauge(FlowlyInstrumentationConstants.DeadLettersPending, () => Interlocked.Read(ref _pendingCount));
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }

    public void UpdatePendingCount(long count)
    {
        Interlocked.Exchange(ref _pendingCount, count);
    }
}