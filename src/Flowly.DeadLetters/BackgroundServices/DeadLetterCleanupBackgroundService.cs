using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flowly.DeadLetters.BackgroundServices;

internal class DeadLetterCleanupBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<DeadLetterTrackingOptions> options,
    IDeadLetterCleanupInstrumentation instrumentation,
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

                if (!IsCleanupConfigured())
                    continue;

                await using var scope = serviceScopeFactory.CreateAsyncScope();
                await RunCleanup(scope.ServiceProvider.GetRequiredService<IDeadLetterRepository>(), stoppingToken);
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

    private bool IsCleanupConfigured()
    {
        var opts = options.Value;

        return opts.DeleteRequeuedMessagesAfter.HasValue || opts.DeleteDeadLetteredMessagesAfter.HasValue;
    }

    internal async Task RunCleanup(IDeadLetterRepository repository, CancellationToken cancellationToken)
    {
        var opts = options.Value;

        if (opts.DeleteRequeuedMessagesAfter.HasValue)
        {
            var requeuedDeleted = await repository.DeleteRequeuedOlderThan(opts.DeleteRequeuedMessagesAfter.Value, cancellationToken);

            if (requeuedDeleted > 0)
                instrumentation.RecordRequeuedPurged(requeuedDeleted);
        }

        if (opts.DeleteDeadLetteredMessagesAfter.HasValue)
        {
            var pendingDeleted = await repository.DeletePendingOlderThan(opts.DeleteDeadLetteredMessagesAfter.Value, cancellationToken);

            if (pendingDeleted > 0)
                instrumentation.RecordPendingPurged(pendingDeleted);
        }
    }
}
