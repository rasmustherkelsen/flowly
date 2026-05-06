namespace Flowly.Jobs.Telemetry;

internal interface IJobStateMetrics
{
    void UpdateCounts(long failed, long running);
}
