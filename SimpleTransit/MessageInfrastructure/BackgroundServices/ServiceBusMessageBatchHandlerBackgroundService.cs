using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using System.Text.Json;
using SimpleTransit.AzureServiceBusWrappers;
using Microsoft.Extensions.Logging;

namespace SimpleTransit.MessageInfrastructure.BackgroundServices;

internal class ServiceBusMessageBatchHandlerBackgroundService<TMessage>(
    IServiceBusClient serviceBusClient,
    ServiceBusMessageBatchHandlerBackgroundService<TMessage>.BatchQueueSettings batchQueueSettings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ServiceBusMessageBatchHandlerBackgroundService<TMessage>> logger) : BackgroundService where TMessage : class
{
    public record BatchQueueSettings(string QueueName, int MaxMessagesBeforeProcessing, TimeSpan MaxWaitTime);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var messageHandlerForLog = scope.ServiceProvider.GetRequiredService<IBatchMessageHandler<TMessage>>();
            logger.LogInformation($"{messageHandlerForLog.GetType().Name} batch waiting for messages on queue '{batchQueueSettings.QueueName}'");
        }

        var receiver = serviceBusClient.CreateReceiver(batchQueueSettings.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var receivedMessages = await receiver.ReceiveMessagesAsync(batchQueueSettings.MaxMessagesBeforeProcessing, batchQueueSettings.MaxWaitTime, stoppingToken);

                if (receivedMessages.Count == 0)
                {
                    continue;
                }

                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var messageHandler = scope.ServiceProvider.GetRequiredService<IBatchMessageHandler<TMessage>>();

                logger.LogInformation($"{messageHandler.GetType().Name} received {receivedMessages.Count} messages");


                await messageHandler.Handle(new BatchMessageContext<TMessage>(receivedMessages.Select(rm => JsonSerializer.Deserialize<TMessage>(rm.Body.ToString())!).ToList(), stoppingToken));

                foreach (var message in receivedMessages)
                {
                    await receiver.CompleteMessageAsync(message, stoppingToken);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e.Message, e);
            }
        }
    }
}
