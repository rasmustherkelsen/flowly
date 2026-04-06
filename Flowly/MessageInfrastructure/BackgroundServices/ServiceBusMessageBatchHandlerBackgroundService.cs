using System.Diagnostics;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

internal class ServiceBusMessageBatchHandlerBackgroundService<TMessage>(
    IMessageBusClient messageBusClient,
    ServiceBusMessageBatchHandlerBackgroundService<TMessage>.BatchQueueSettings batchQueueSettings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ServiceBusMessageBatchHandlerBackgroundService<TMessage>> logger,
    HandlerInstrumentation handlerInstrumentation) : BackgroundService where TMessage : class
{
    public record BatchQueueSettings(string QueueName, int MaxMessagesBeforeProcessing, TimeSpan MaxWaitTime);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var messageHandlerForLog = scope.ServiceProvider.GetRequiredService<BatchMessageHandlerBase<TMessage>>();
            logger.LogInformation("{MessageHandlerName} batch waiting for messages on queue '{QueueName}'", messageHandlerForLog.GetType().Name, batchQueueSettings.QueueName);
        }

        await using var receiver = await messageBusClient.CreateReceiver(batchQueueSettings.QueueName);

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
                var handlerName = messageHandler.GetType().Name;

                handlerInstrumentation.RecordReceived(handlerName, batchQueueSettings.QueueName, receivedMessages.Count);
                logger.LogInformation("{MessageHandlerName} received {ReceivedMessagesCount} messages", handlerName, receivedMessages.Count);

                var sw = Stopwatch.StartNew();
                using var activity = handlerInstrumentation.StartHandling(handlerName, batchQueueSettings.QueueName);

                try
                {
                    await messageHandler.Handle(new BatchMessageContext<TMessage>(receivedMessages.Select(rm => rm.Body).ToList(), stoppingToken));
                    await receiver.CompleteMessages(receivedMessages, stoppingToken);
                    handlerInstrumentation.RecordSucceeded(handlerName, batchQueueSettings.QueueName, sw.Elapsed.TotalMilliseconds, receivedMessages.Count);
                    logger.LogInformation("{MessageHandlerName} completed {ReceivedMessagesCount} messages", handlerName, receivedMessages.Count);
                }
                catch (Exception e)
                {
                    handlerInstrumentation.RecordFailed(handlerName, batchQueueSettings.QueueName, receivedMessages.Count);
                    logger.LogError(e.Message, e);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e.Message, e);
            }
        }
    }
}
