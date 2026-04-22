using Flowly.DeadLetters.Repositories;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.BackgroundServices;

internal class DeadLetterIngestionHealthBackgroundService(
    IMessageBusClientRegistry clientRegistry,
    IEnumerable<DeadLetterIngestionSettings> queueSettings,
    IEnumerable<EventSubscriptionDeadLetterIngestionSettings> eventSubscriptionSettings,
    DeadLetterIngestionHealthSettings healthSettings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DeadLetterIngestionHealthBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await Task.Delay(healthSettings.CheckInterval, stoppingToken);
                await CheckAll(stoppingToken);
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

    private async Task CheckAll(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeadLetterRepository>();

        foreach (var settings in queueSettings) await CheckQueue(settings.QueueName, settings.ProviderName, repository, cancellationToken);

        foreach (var settings in eventSubscriptionSettings) await CheckEventSubscription(settings, repository, cancellationToken);
    }

    private async Task CheckQueue(string queueName, string providerName, IDeadLetterRepository repository, CancellationToken cancellationToken)
    {
        var client = clientRegistry.GetClient(providerName);
        var messageCount = await client.GetDeadLetterMessageCount(queueName, cancellationToken);

        if (messageCount == 0)
            return;

        var lastIngestion = await repository.GetLastIngestionTime(queueName, cancellationToken);
        var stalled = lastIngestion is null || lastIngestion < DateTimeOffset.UtcNow - healthSettings.StallThreshold;

        if (stalled)
            logger.LogWarning(
                "Dead letter ingestion appears stalled for queue '{QueueName}': {MessageCount} message(s) on the dead letter queue, last ingestion: {LastIngestion}",
                queueName,
                messageCount,
                lastIngestion?.ToString("O") ?? "never");
    }

    private async Task CheckEventSubscription(EventSubscriptionDeadLetterIngestionSettings settings, IDeadLetterRepository repository, CancellationToken cancellationToken)
    {
        var client = clientRegistry.GetClient(settings.ProviderName);

        if (client is not IEventCapableMessageBusClient eventCapableClient)
            return;

        var messageCount = await eventCapableClient.GetEventSubscriptionDeadLetterMessageCount(
            settings.TopicOrExchangeName,
            settings.SubscriptionName,
            cancellationToken);

        if (messageCount == 0)
            return;

        var lastIngestion = await repository.GetLastIngestionTimeForSubscription(
            settings.TopicOrExchangeName,
            settings.SubscriptionName,
            cancellationToken);

        var stalled = lastIngestion is null || lastIngestion < DateTimeOffset.UtcNow - healthSettings.StallThreshold;

        if (stalled)
            logger.LogWarning(
                "Dead letter ingestion appears stalled for event subscription '{DisplayName}': {MessageCount} message(s) on the dead letter queue, last ingestion: {LastIngestion}",
                settings.DisplayName,
                messageCount,
                lastIngestion?.ToString("O") ?? "never");
    }
}