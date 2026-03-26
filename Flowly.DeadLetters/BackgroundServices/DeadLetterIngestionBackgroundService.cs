using Flowly.DeadLetters.Repositories;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.BackgroundServices;

internal class DeadLetterIngestionBackgroundService(
    IMessageBusClient messageBusClient,
    DeadLetterIngestionSettings settings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DeadLetterIngestionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Dead letter ingestion started for queue '{QueueName}'", settings.QueueName);

        await using var receiver = messageBusClient.CreateDeadLetterReceiver(settings.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await receiver.ReceiveMessages(50, PollInterval, stoppingToken);

                if (messages.Count > 0)
                    await ProcessBatch(receiver, messages, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dead letter ingestion error for queue '{QueueName}'", settings.QueueName);
            }
        }
    }

    private async Task ProcessBatch(IDeadLetterReceiver receiver, IReadOnlyCollection<IDeadLetterMessage> messages, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDeadLetterRepository>();
            await repository.SaveBatch(messages, settings.QueueName, cancellationToken);

            foreach (var message in messages)
            {
                await receiver.CompleteMessage(message, cancellationToken);
            }

            logger.LogInformation("Dead letter ingestion persisted {Count} messages from queue '{QueueName}'", messages.Count, settings.QueueName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist dead letter batch from queue '{QueueName}', abandoning {Count} messages", settings.QueueName, messages.Count);

            foreach (var message in messages)
            {
                await receiver.AbandonMessage(message, cancellationToken);
            }
        }
    }
}