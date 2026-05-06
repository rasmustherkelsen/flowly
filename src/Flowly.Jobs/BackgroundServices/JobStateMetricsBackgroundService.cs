using Flowly.Jobs.Repositories;
using Flowly.Jobs.Telemetry;
using Microsoft.Extensions.Hosting;

namespace Flowly.Jobs.BackgroundServices;

internal class JobStateMetricsBackgroundService(
    IJobStateCountReader countReader,
    IJobStateMetrics metrics,
    FlowlyOptions options,
    JobStateMetricsBackgroundServiceOptions serviceOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.EnableTelemetry) return;

        using var timer = new PeriodicTimer(serviceOptions.PollingInterval);
        do
        {
            try
            {
                var failedCount = await countReader.CountJobsInState(JobState.Failed, stoppingToken);
                var runningCount = await countReader.CountJobsInState(JobState.Started, stoppingToken);
                metrics.UpdateCounts(failedCount, runningCount);
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                // metrics polling must not affect application health
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}