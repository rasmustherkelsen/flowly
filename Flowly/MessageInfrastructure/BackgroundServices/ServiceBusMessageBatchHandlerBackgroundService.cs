using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

internal class ServiceBusMessageBatchHandlerBackgroundService<TMessage>(
    IMessageBusClient messageBusClient,
    ServiceBusMessageBatchHandlerBackgroundService<TMessage>.BatchQueueSettings batchQueueSettings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ServiceBusMessageBatchHandlerBackgroundService<TMessage>> logger) : BackgroundService where TMessage : class
{
    public record BatchQueueSettings(string QueueName, int MaxMessagesBeforeProcessing, TimeSpan MaxWaitTime);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var messageHandlerForLog = scope.ServiceProvider.GetRequiredService<BatchMessageHandlerBase<TMessage>>();
            logger.LogInformation("{MessageHandlerName} batch waiting for messages on queue '{QueueName}'", messageHandlerForLog.GetType().Name, batchQueueSettings.QueueName);
        }

        await using var receiver = messageBusClient.CreateReceiver(batchQueueSettings.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var receivedMessages = await receiver.ReceiveMessages<TMessage>(batchQueueSettings.MaxMessagesBeforeProcessing, batchQueueSettings.MaxWaitTime, stoppingToken);

                if (receivedMessages.Count == 0)
                {
                    continue;
                }

                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var messageHandler = scope.ServiceProvider.GetRequiredService<BatchMessageHandlerBase<TMessage>>();

                logger.LogInformation("{MessageHandlerName} received {ReceivedMessagesCount} messages", messageHandler.GetType().Name, receivedMessages.Count);

                await messageHandler.Handle(new BatchMessageContext<TMessage>(receivedMessages.Select(rm => rm.Body).ToList(), stoppingToken));

                await receiver.CompleteMessages(receivedMessages, stoppingToken);
                
                logger.LogInformation("{MessageHandlerName} completed {ReceivedMessagesCount} messages", messageHandler.GetType().Name, receivedMessages.Count);
            }
            catch (Exception e)
            {
                logger.LogError(e.Message, e);
            }
        }
    }
}