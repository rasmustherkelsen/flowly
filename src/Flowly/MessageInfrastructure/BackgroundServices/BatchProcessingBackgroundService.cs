using System.Diagnostics;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

internal class BatchProcessingBackgroundService<TMessage>(
    IMessageBusClientRegistry clientRegistry,
    IHandlerSettings<TMessage> handlerSettings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<BatchProcessingBackgroundService<TMessage>> logger,
    IHandlerInstrumentation handlerInstrumentation) : MessageProcessingBackgroundServiceBase<TMessage> where TMessage : class
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var messageHandlerForLog = scope.ServiceProvider.GetRequiredService<BatchMessageHandler<TMessage>>();
            logger.LogInformation("{MessageHandlerName} batch waiting for messages on queue '{QueueName}'", messageHandlerForLog.GetType().Name, handlerSettings.QueueName);
        }

        var client = clientRegistry.GetClient(handlerSettings.ProviderName);
        var messagingSystem = client.MessagingSystem;
        await using var receiver = await client.CreateReceiver(handlerSettings.QueueName);

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var receivedMessages = await receiver.ReceiveMessages<TMessage>(handlerSettings.MaxMessagesBeforeProcessing, handlerSettings.MaxWaitTime, stoppingToken);

                if (receivedMessages.Count == 0) continue;

                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var messageHandler = scope.ServiceProvider.GetRequiredService<BatchMessageHandler<TMessage>>();
                var handlerName = messageHandler.GetType().Name;

                handlerInstrumentation.RecordReceived(handlerName, handlerSettings.QueueName, receivedMessages.Count);
                logger.LogInformation("{MessageHandlerName} received {ReceivedMessagesCount} messages", handlerName, receivedMessages.Count);

                var sw = Stopwatch.StartNew();
                using var activity = handlerInstrumentation.StartHandling(handlerName, handlerSettings.QueueName, messagingSystem, MessageProperties.Empty);

                try
                {
                    await messageHandler.Handle(new BatchMessageContext<TMessage>(receivedMessages.Select(rm => rm.Body).ToList(), stoppingToken));
                    await receiver.CompleteMessages(receivedMessages, stoppingToken);
                    handlerInstrumentation.RecordSucceeded(handlerName, handlerSettings.QueueName, sw.Elapsed.TotalMilliseconds, receivedMessages.Count);
                    logger.LogInformation("{MessageHandlerName} completed {ReceivedMessagesCount} messages", handlerName, receivedMessages.Count);
                }
                catch (Exception e)
                {
                    handlerInstrumentation.RecordFailed(handlerName, handlerSettings.QueueName, receivedMessages.Count);
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