using Flowly.Jobs.DatabaseModel;
using Flowly.Jobs.Model;
using Flowly.Jobs.Telemetry;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Flowly.Jobs.BackgroundServices;

internal class JobStateMetricsBackgroundService(
    IDbContextFactory<JobStateDataContext> dbContextFactory,
    JobStateGaugeMetrics metrics,
    FlowlyOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.EnableTelemetry) return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        do
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);
                var failedCount = await dbContext.Jobs.LongCountAsync(j => j.CurrentState == JobState.Failed, stoppingToken);
                var runningCount = await dbContext.Jobs.LongCountAsync(j => j.CurrentState == JobState.Started, stoppingToken);
                metrics.UpdateCounts(failedCount, runningCount);
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                // metrics polling must not affect application health
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}