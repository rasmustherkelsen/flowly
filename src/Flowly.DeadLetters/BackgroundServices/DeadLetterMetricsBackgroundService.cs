using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Flowly.DeadLetters.BackgroundServices;

internal class DeadLetterMetricsBackgroundService(
    IDbContextFactory<DeadLetterDataContext> dbContextFactory,
    DeadLetterGaugeMetrics metrics,
    FlowlyOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.EnableTelemetry) return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        do
        {
            await RefreshPendingCount(stoppingToken);
        } 
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task RefreshPendingCount(CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var pendingCount = await dbContext.DeadLetters.LongCountAsync(d => d.Status == DeadLetterStatus.Pending, cancellationToken);
            metrics.UpdatePendingCount(pendingCount);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // metrics polling must not affect application health
        }
    }
}