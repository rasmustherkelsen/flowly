using Flowly.DeadLetters.Services;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.BackgroundServices;

internal class EventSubscriptionDeadLetterIngestionBackgroundService(
    IMessageBusClientRegistry clientRegistry,
    EventSubscriptionDeadLetterIngestionSettings settings,
    IDeadLetterBatchIngestor batchIngestor,
    ILogger<EventSubscriptionDeadLetterIngestionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Dead letter ingestion started for event subscription '{DisplayName}'",
            settings.DisplayName);

        while (!stoppingToken.IsCancellationRequested)
        {
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
                        await batchIngestor.IngestForEventSubscription(receiver, messages, settings.TopicName, settings.SubscriptionName, settings.DisplayName, stoppingToken);
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
    }
}
