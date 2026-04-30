using Flowly.DeadLetters.Repositories;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.BackgroundServices;

internal class EventSubscriptionDeadLetterIngestionBackgroundService(
    IMessageBusClientRegistry clientRegistry,
    EventSubscriptionDeadLetterIngestionSettings settings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<EventSubscriptionDeadLetterIngestionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Dead letter ingestion started for event subscription '{DisplayName}'",
            settings.DisplayName);

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var client = clientRegistry.GetClient(settings.ProviderName);

                if (client is not IEventCapableMessageBusClient eventCapableClient)
                    throw new InvalidOperationException(
                        $"The message bus client for provider '{settings.ProviderName}' does not support events.");

                await using var receiver = await eventCapableClient.CreateEventSubscriptionDeadLetterReceiver(
                    settings.TopicName,
                    settings.SubscriptionName);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var messages = await receiver.ReceiveMessages(50, PollInterval, stoppingToken);

                    if (messages.Count > 0)
                        await ProcessBatch(receiver, messages, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Dead letter ingestion error for event subscription '{DisplayName}', restarting receiver",
                    settings.DisplayName);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
    }

    private async Task ProcessBatch(IDeadLetterReceiver receiver, IReadOnlyCollection<IDeadLetterMessage> messages, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDeadLetterRepository>();

            await repository.SaveBatchForSubscription(
                messages,
                settings.TopicName,
                settings.SubscriptionName,
                cancellationToken);

            foreach (var message in messages) await receiver.CompleteMessage(message, cancellationToken);

            logger.LogInformation(
                "Dead letter ingestion persisted {Count} messages from event subscription '{DisplayName}'",
                messages.Count,
                settings.DisplayName);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to persist dead letter batch from event subscription '{DisplayName}', abandoning {Count} messages",
                settings.DisplayName,
                messages.Count);

            foreach (var message in messages) await receiver.AbandonMessage(message, cancellationToken);
        }
    }
}