using System.Diagnostics;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
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

                if (handlerSettings.MaxRetries == 0)
                    await HandleAtMostOnce(messageHandler, handlerName, receivedMessages, receiver, sw, stoppingToken);
                else
                    await HandleWithRetry(messageHandler, handlerName, receivedMessages, receiver, sw, stoppingToken);
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

    private async Task HandleAtMostOnce(
        BatchMessageHandler<TMessage> messageHandler,
        string handlerName,
        IReadOnlyCollection<IReceivedMessage<TMessage>> receivedMessages,
        IMessageBusReceiver receiver,
        Stopwatch sw,
        CancellationToken cancellationToken)
    {
        await receiver.CompleteMessages(receivedMessages, cancellationToken);

        try
        {
            await messageHandler.Handle(new BatchMessageContext<TMessage>(receivedMessages.Select(rm => rm.Body).ToList(), cancellationToken));
            handlerInstrumentation.RecordSucceeded(handlerName, handlerSettings.QueueName, sw.Elapsed.TotalMilliseconds, receivedMessages.Count);
            logger.LogInformation("{MessageHandlerName} completed {ReceivedMessagesCount} messages", handlerName, receivedMessages.Count);
        }
        catch (Exception e)
        {
            handlerInstrumentation.RecordFailed(handlerName, handlerSettings.QueueName, receivedMessages.Count);
            logger.LogError(e, "{MessageHandlerName} failed processing {ReceivedMessagesCount} messages (at-most-once — messages will not be redelivered)", handlerName, receivedMessages.Count);
        }
    }

    private async Task HandleWithRetry(
        BatchMessageHandler<TMessage> messageHandler,
        string handlerName,
        IReadOnlyCollection<IReceivedMessage<TMessage>> receivedMessages,
        IMessageBusReceiver receiver,
        Stopwatch sw,
        CancellationToken cancellationToken)
    {
        try
        {
            await messageHandler.Handle(new BatchMessageContext<TMessage>(receivedMessages.Select(rm => rm.Body).ToList(), cancellationToken));
            await receiver.CompleteMessages(receivedMessages, cancellationToken);
            handlerInstrumentation.RecordSucceeded(handlerName, handlerSettings.QueueName, sw.Elapsed.TotalMilliseconds, receivedMessages.Count);
            logger.LogInformation("{MessageHandlerName} completed {ReceivedMessagesCount} messages", handlerName, receivedMessages.Count);
        }
        catch (Exception e)
        {
            var currentRetry = receivedMessages.Max(m => m.Properties.RetryCount);

            if (currentRetry < handlerSettings.MaxRetries)
            {
                await RepublishBatchForRetry(receivedMessages, currentRetry + 1, cancellationToken);
                handlerInstrumentation.RecordRetried(handlerName, handlerSettings.QueueName, receivedMessages.Count);
                logger.LogWarning(e, "{MessageHandlerName} batch handling failed, retrying {Count} messages (attempt {Next}/{Max})",
                    handlerName, receivedMessages.Count, currentRetry + 1, handlerSettings.MaxRetries);
            }
            else
            {
                handlerInstrumentation.RecordFailed(handlerName, handlerSettings.QueueName, receivedMessages.Count);
                logger.LogError(e, "{MessageHandlerName} batch handling failed after {MaxRetries} retries, discarding {Count} messages",
                    handlerName, handlerSettings.MaxRetries, receivedMessages.Count);
            }

            await receiver.CompleteMessages(receivedMessages, cancellationToken);
        }
    }

    private async Task RepublishBatchForRetry(IReadOnlyCollection<IReceivedMessage<TMessage>> messages, int retryCount, CancellationToken cancellationToken)
    {
        var scheduledTime = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(handlerSettings.RetryDelaySeconds);
        var client = clientRegistry.GetClient(handlerSettings.ProviderName);
        var sender = await client.CreateMessageBusSender(handlerSettings.QueueName);

        await Task.WhenAll(messages.Select(msg =>
        {
            var props = msg.Properties with { RetryCount = retryCount, ScheduledEnqueueTime = scheduledTime };
            return sender.SendMessage(msg.Body, props, cancellationToken);
        }));
    }
}