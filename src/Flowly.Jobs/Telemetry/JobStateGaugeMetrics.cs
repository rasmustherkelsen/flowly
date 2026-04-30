using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Jobs.Telemetry;

internal sealed class JobStateGaugeMetrics : IDisposable
{
    private readonly Meter? _meter;
    private long _failedCount;
    private long _runningCount;

    public JobStateGaugeMetrics(FlowlyOptions options)
    {
        if (!options.EnableTelemetry) return;

        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _meter.CreateObservableGauge(FlowlyInstrumentationConstants.JobsFailed, () => Interlocked.Read(ref _failedCount));
        _meter.CreateObservableGauge(FlowlyInstrumentationConstants.JobsRunning, () => Interlocked.Read(ref _runningCount));
    }

    public void UpdateCounts(long failed, long running)
    {
        Interlocked.Exchange(ref _failedCount, failed);
        Interlocked.Exchange(ref _runningCount, running);
    }

    public void Dispose() => _meter?.Dispose();
}
