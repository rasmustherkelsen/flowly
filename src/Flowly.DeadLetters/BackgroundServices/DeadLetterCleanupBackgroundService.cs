using Flowly.DeadLetters.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flowly.DeadLetters.BackgroundServices;

internal class DeadLetterCleanupBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<DeadLetterTrackingOptions> options,
    ILogger<DeadLetterCleanupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
                await RunCleanup(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dead letter cleanup failed");
            }
        }
    }

    private async Task RunCleanup(CancellationToken cancellationToken)
    {
        var opts = options.Value;

        if (!opts.DeleteRequeuedMessagesAfter.HasValue && !opts.DeleteDeadLetteredMessagesAfter.HasValue)
            return;

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeadLetterRepository>();

        if (opts.DeleteRequeuedMessagesAfter.HasValue)
            await repository.DeleteRequeuedOlderThan(opts.DeleteRequeuedMessagesAfter.Value, cancellationToken);

        if (opts.DeleteDeadLetteredMessagesAfter.HasValue)
            await repository.DeletePendingOlderThan(opts.DeleteDeadLetteredMessagesAfter.Value, cancellationToken);
    }
}
