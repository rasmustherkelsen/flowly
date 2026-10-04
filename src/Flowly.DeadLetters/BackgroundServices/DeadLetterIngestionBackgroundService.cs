using Flowly.DeadLetters.Services;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.BackgroundServices;

internal class DeadLetterIngestionBackgroundService(
    IMessageBusClientRegistry clientRegistry,
    DeadLetterIngestionSettings settings,
    IDeadLetterBatchIngestor batchIngestor,
    ILogger<DeadLetterIngestionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Dead letter ingestion started for queue '{QueueName}'", settings.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = clientRegistry.GetClient(settings.ProviderName);
                await using var receiver = await client.CreateDeadLetterReceiver(settings.QueueName);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var messages = await receiver.ReceiveMessages(50, PollInterval, stoppingToken);

                    if (messages.Count > 0)
                        await batchIngestor.IngestForQueue(receiver, messages, settings.QueueName, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dead letter ingestion error for queue '{QueueName}', restarting receiver", settings.QueueName);
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
