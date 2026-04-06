using Flowly.DeadLetters.Repositories;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.BackgroundServices;

internal class DeadLetterIngestionHealthBackgroundService(
    IMessageBusClient messageBusClient,
    IEnumerable<DeadLetterIngestionSettings> queueSettings,
    DeadLetterIngestionHealthSettings healthSettings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DeadLetterIngestionHealthBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(healthSettings.CheckInterval, stoppingToken);
                await CheckAllQueues(stoppingToken);
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

    private async Task CheckAllQueues(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeadLetterRepository>();

        foreach (var settings in queueSettings)
        {
            await CheckQueue(settings.QueueName, repository, cancellationToken);
        }
    }

    private async Task CheckQueue(string queueName, IDeadLetterRepository repository, CancellationToken cancellationToken)
    {
        var messageCount = await messageBusClient.GetDeadLetterMessageCount(queueName, cancellationToken);

        if (messageCount == 0)
            return;

        var lastIngestion = await repository.GetLastIngestionTime(queueName, cancellationToken);
        var stalled = lastIngestion is null || lastIngestion < DateTimeOffset.UtcNow - healthSettings.StallThreshold;

        if (stalled)
        {
            logger.LogWarning(
                "Dead letter ingestion appears stalled for queue '{QueueName}': {MessageCount} message(s) on the dead letter queue, last ingestion: {LastIngestion}",
                queueName,
                messageCount,
                lastIngestion?.ToString("O") ?? "never");
        }
    }
}
