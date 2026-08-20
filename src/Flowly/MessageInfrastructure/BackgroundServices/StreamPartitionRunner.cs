using System.Diagnostics;
using System.Threading.Channels;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

/// <summary>
///     Accumulates batches from a single <see cref="IMessageBusProcessor{TMessage}" /> and runs them through the
///     handler with in-process retry, halt-on-exhaustion, and checkpoint semantics. One instance per partition —
///     <see cref="MessageStreamProcessingBackgroundService{TMessage,THandler}" /> creates exactly one with
///     <c>partition: null</c>; <see cref="PartitionedMessageStreamProcessingBackgroundService{TMessage,THandler}" />
///     creates one per partition currently owned by this process. Batches never mix partitions — this is what
///     preserves per-partition ordering guarantees at the handler's <see cref="IMessageStreamContext{T}" />.
/// </summary>
internal sealed class StreamPartitionRunner<TMessage, THandler>(
    IMessageBusProcessor<TMessage> processor,
    int? partition,
    string handlerName,
    string queueName,
    string consumerName,
    int maxMessagesBeforeProcessing,
    TimeSpan maxWaitTime,
    int maxRetries,
    int retryDelaySeconds,
    string messagingSystem,
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger,
    IHandlerInstrumentation handlerInstrumentation)
    where TMessage : class
    where THandler : MessageStreamHandler<TMessage>
{
    private readonly Channel<IReceivedMessage<TMessage>> _buffer = Channel.CreateUnbounded<IReceivedMessage<TMessage>>();

    public async Task Run(CancellationToken stoppingToken)
    {
        processor.ProcessMessage += (receivedMessage, _) =>
        {
            _buffer.Writer.TryWrite(receivedMessage);
            return Task.CompletedTask;
        };

        processor.ProcessError += errorDetails =>
        {
            logger.LogError(errorDetails.Exception, "{HandlerName} stream transport error on queue '{QueueName}' partition {Partition}", handlerName, queueName, partition);
            return Task.CompletedTask;
        };

        await processor.StartProcessingMessages(stoppingToken);
        logger.LogInformation("{HandlerName} waiting for stream messages on queue '{QueueName}' partition {Partition}", handlerName, queueName, partition);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = await AccumulateBatch(stoppingToken);
                if (batch.Count == 0) continue;

                var halted = !await ProcessBatch(batch, stoppingToken);
                if (!halted) continue;

                await processor.StopProcessing(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unexpected error in stream message processing loop for queue '{QueueName}' partition {Partition}", queueName, partition);
            }
        }
    }

    public Task StopProcessing(CancellationToken cancellationToken) => processor.StopProcessing(cancellationToken);

    public ValueTask DisposeAsync() => processor.DisposeAsync();

    private async Task<List<IReceivedMessage<TMessage>>> AccumulateBatch(CancellationToken stoppingToken)
    {
        var batch = new List<IReceivedMessage<TMessage>> { await _buffer.Reader.ReadAsync(stoppingToken) };

        if (maxMessagesBeforeProcessing <= 1)
            return batch;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeoutCts.CancelAfter(maxWaitTime);

        try
        {
            while (batch.Count < maxMessagesBeforeProcessing)
                batch.Add(await _buffer.Reader.ReadAsync(timeoutCts.Token));
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
        }

        return batch;
    }

    private async Task<bool> ProcessBatch(IReadOnlyList<IReceivedMessage<TMessage>> batch, CancellationToken stoppingToken)
    {
        handlerInstrumentation.RecordReceived(handlerName, queueName, batch.Count);

        var links = batch
            .Select(m => ActivityContextParser.Parse(m.Properties))
            .Where(ctx => ctx != default)
            .Select(ctx => new ActivityLink(ctx))
            .ToList();

        var sw = Stopwatch.StartNew();
        using var activity = handlerInstrumentation.StartHandling(handlerName, queueName, messagingSystem, batch[0].Properties, links);

        for (var attempt = 0;; attempt++)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var messageHandler = scope.ServiceProvider.GetRequiredService<THandler>();

            try
            {
                await messageHandler.Handle(new MessageStreamContext<TMessage>(batch.Select(m => m.Body).ToList(), stoppingToken, partition));

                foreach (var receivedMessage in batch)
                    await receivedMessage.Complete(stoppingToken);

                await SaveCheckpointIfPresent(batch, stoppingToken);

                handlerInstrumentation.RecordSucceeded(handlerName, queueName, sw.Elapsed.TotalMilliseconds, batch.Count);
                logger.LogInformation("{HandlerName} completed {Count} stream messages (partition {Partition})", handlerName, batch.Count, partition);
                return true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                if (attempt < maxRetries)
                {
                    handlerInstrumentation.RecordRetried(handlerName, queueName, batch.Count);
                    logger.LogWarning(e, "{HandlerName} stream batch handling failed, retrying in-process (attempt {Next}/{Max}) — messages are not re-published",
                        handlerName, attempt + 1, maxRetries);

                    if (retryDelaySeconds > 0)
                        await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), stoppingToken);

                    continue;
                }

                handlerInstrumentation.RecordHalted(handlerName, queueName, e.Message);
                logger.LogCritical(e,
                    "{HandlerName} exhausted {MaxRetries} in-process retries on stream queue '{QueueName}' partition {Partition} — halting consumption. " +
                    "The failed batch is not acknowledged, the stream offset will not advance past it, and no further messages will be processed. " +
                    "Manual intervention required: fix the handler and restart the process.",
                    handlerName, maxRetries, queueName, partition);
                return false;
            }
        }
    }

    private async Task SaveCheckpointIfPresent(IReadOnlyList<IReceivedMessage<TMessage>> batch, CancellationToken cancellationToken)
    {
        var lastOffset = batch[^1].Properties.StreamOffset;
        if (lastOffset is null) return;

        await MessageStreamCheckpointHelper.SaveStreamPositionIfPresent<TMessage>(serviceScopeFactory, consumerName, partition, lastOffset.Value, cancellationToken);
    }
}
