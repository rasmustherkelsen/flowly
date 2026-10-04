using Flowly.DeadLetters.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.BackgroundServices;

internal class DeadLetterIngestionHealthBackgroundService(
    DeadLetterIngestionHealthSettings healthSettings,
    IDeadLetterIngestionHealthChecker healthChecker,
    ILogger<DeadLetterIngestionHealthBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(healthSettings.CheckInterval, stoppingToken);
                await healthChecker.CheckAll(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dead letter ingestion health check failed");
            }
        }
    }
}
