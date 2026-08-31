using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.DeadLetters.Telemetry;

internal sealed class DeadLetterCleanupInstrumentation : IDeadLetterCleanupInstrumentation, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _pendingPurged;
    private readonly Counter<long> _requeuedPurged;

    public DeadLetterCleanupInstrumentation()
    {
        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _pendingPurged = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.DeadLetterCleanupPendingPurged);
        _requeuedPurged = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.DeadLetterCleanupRequeuedPurged);
    }

    public void RecordPendingPurged(int count) => _pendingPurged.Add(count);

    public void RecordRequeuedPurged(int count) => _requeuedPurged.Add(count);

    public void Dispose() => _meter.Dispose();
}
