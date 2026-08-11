using System.Diagnostics;
using System.Threading.Channels;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

internal sealed class MessageStreamProcessingBackgroundService<TMessage, THandler>(
    IMessageBusClientRegistry clientRegistry,
    MessageStreamHandlerSettings<TMessage, THandler> handlerSettings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<MessageStreamProcessingBackgroundService<TMessage, THandler>> logger,
    IHandlerInstrumentation handlerInstrumentation) : MessageProcessingBackgroundServiceBase<TMessage>
    where TMessage : class
    where THandler : MessageStreamHandler<TMessage>
{
    private readonly Channel<IReceivedMessage<TMessage>> _buffer = Channel.CreateUnbounded<IReceivedMessage<TMessage>>();
    private IMessageBusProcessor<TMessage>? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = clientRegistry.GetClient(handlerSettings.ProviderName);

        if (client is not IStreamCapableMessageBusClient streamCapableClient)
            throw new InvalidOperationException(
                $"The message bus client for provider '{handlerSettings.ProviderName}' does not support message streaming. " +
                $"The client must implement {nameof(IStreamCapableMessageBusClient)}.");

        var messagingSystem = client.MessagingSystem;

        _processor = await streamCapableClient.CreateStreamProcessor<TMessage>(
            handlerSettings.QueueName,
            handlerSettings.StartPosition,
            new MessageBusProcessorOptions(handlerSettings.MaxMessagesBeforeProcessing, MessageBusReceiveMode.PeekLock));

        _processor.ProcessMessage += (receivedMessage, _) =>
        {
            _buffer.Writer.TryWrite(receivedMessage);
            return Task.CompletedTask;
        };

        _processor.ProcessError += errorDetails =>
        {
            logger.LogError(errorDetails.Exception, "{HandlerName} stream transport error on queue '{QueueName}'", handlerSettings.HandlerName, handlerSettings.QueueName);
            return Task.CompletedTask;
        };

        await _processor.StartProcessingMessages(stoppingToken);
        logger.LogInformation("{HandlerName} waiting for stream messages on queue '{QueueName}'", handlerSettings.HandlerName, handlerSettings.QueueName);

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var batch = await AccumulateBatch(stoppingToken);
                if (batch.Count == 0) continue;

                var halted = !await ProcessBatch(batch, messagingSystem, stoppingToken);
                if (!halted) continue;

                await _processor.StopProcessing(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unexpected error in stream message processing loop for queue '{QueueName}'", handlerSettings.QueueName);
            }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            await _processor.StopProcessing(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task<List<IReceivedMessage<TMessage>>> AccumulateBatch(CancellationToken stoppingToken)
    {
        var batch = new List<IReceivedMessage<TMessage>> { await _buffer.Reader.ReadAsync(stoppingToken) };

        if (handlerSettings.MaxMessagesBeforeProcessing <= 1)
            return batch;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeoutCts.CancelAfter(handlerSettings.MaxWaitTime);

        try
        {
            while (batch.Count < handlerSettings.MaxMessagesBeforeProcessing)
                batch.Add(await _buffer.Reader.ReadAsync(timeoutCts.Token));
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
        }

        return batch;
    }

    private async Task<bool> ProcessBatch(IReadOnlyList<IReceivedMessage<TMessage>> batch, string messagingSystem, CancellationToken stoppingToken)
    {
        handlerInstrumentation.RecordReceived(handlerSettings.HandlerName, handlerSettings.QueueName, batch.Count);

        var links = batch
            .Select(m => ActivityContextParser.Parse(m.Properties))
            .Where(ctx => ctx != default)
            .Select(ctx => new ActivityLink(ctx))
            .ToList();

        var sw = Stopwatch.StartNew();
        using var activity = handlerInstrumentation.StartHandling(handlerSettings.HandlerName, handlerSettings.QueueName, messagingSystem, batch[0].Properties, links);

        for (var attempt = 0;; attempt++)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var messageHandler = scope.ServiceProvider.GetRequiredService<THandler>();

            try
            {
                await messageHandler.Handle(new MessageStreamContext<TMessage>(batch.Select(m => m.Body).ToList(), stoppingToken));

                foreach (var receivedMessage in batch)
                    await receivedMessage.Complete(stoppingToken);

                handlerInstrumentation.RecordSucceeded(handlerSettings.HandlerName, handlerSettings.QueueName, sw.Elapsed.TotalMilliseconds, batch.Count);
                logger.LogInformation("{HandlerName} completed {Count} stream messages", handlerSettings.HandlerName, batch.Count);
                return true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                if (attempt < handlerSettings.MaxRetries)
                {
                    handlerInstrumentation.RecordRetried(handlerSettings.HandlerName, handlerSettings.QueueName, batch.Count);
                    logger.LogWarning(e, "{HandlerName} stream batch handling failed, retrying in-process (attempt {Next}/{Max}) — messages are not re-published",
                        handlerSettings.HandlerName, attempt + 1, handlerSettings.MaxRetries);

                    if (handlerSettings.RetryDelaySeconds > 0)
                        await Task.Delay(TimeSpan.FromSeconds(handlerSettings.RetryDelaySeconds), stoppingToken);

                    continue;
                }

                handlerInstrumentation.RecordHalted(handlerSettings.HandlerName, handlerSettings.QueueName, e.Message);
                logger.LogCritical(e,
                    "{HandlerName} exhausted {MaxRetries} in-process retries on stream queue '{QueueName}' — halting consumption. " +
                    "The failed batch is not acknowledged, the stream offset will not advance past it, and no further messages will be processed. " +
                    "Manual intervention required: fix the handler and restart the process.",
                    handlerSettings.HandlerName, handlerSettings.MaxRetries, handlerSettings.QueueName);
                return false;
            }
        }
    }
}