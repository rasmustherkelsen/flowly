using System.Diagnostics;
using System.Threading.Channels;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.StreamPartitions;
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
///     <para>
///         A batch that exhausts its in-process retries is held, not dropped: the runner stays alive, keeps the failed batch and its
///         processor, and waits for <see cref="Resume" />. Meanwhile it reads nothing from its bounded buffer, so the buffer fills and
///         the processor's <see cref="IMessageBusProcessor{TMessage}.ProcessMessage" /> delegate stops completing — the transport awaits
///         it, stops receiving, and the broker keeps the rest of the stream. <see cref="Resume" /> re-runs the held batch in place with
///         a fresh set of retries. Cancelling the token passed to <see cref="Run" /> ends a held runner.
///     </para>
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
    IHandlerInstrumentation handlerInstrumentation,
    StreamPartitionStateSession stateSession,
    IStreamPartitionResumeRegistry resumeRegistry) : IResumableStreamPartition
    where TMessage : class
    where THandler : MessageStreamHandler<TMessage>
{
    private readonly Channel<IReceivedMessage<TMessage>> _buffer = Channel.CreateBounded<IReceivedMessage<TMessage>>(
        new BoundedChannelOptions(Math.Max(1, maxMessagesBeforeProcessing))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    private readonly Lock _resumeLock = new();
    private TaskCompletionSource? _resumeSignal;

    public async Task Run(CancellationToken stoppingToken)
    {
        using var resumeRegistration = resumeRegistry.Register(new StreamPartitionKey(queueName, consumerName, partition), this);

        processor.ProcessMessage += (receivedMessage, transportToken) => Buffer(receivedMessage, transportToken, stoppingToken);

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

                handlerInstrumentation.RecordReceived(handlerName, queueName, batch.Count);

                while (await ProcessBatch(batch, stoppingToken) is { } resumed)
                {
                    await HoldUntilResumed(resumed, stoppingToken);
                }
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

    public bool Resume()
    {
        TaskCompletionSource? resumeSignal;
        lock (_resumeLock)
        {
            resumeSignal = _resumeSignal;
            if (resumeSignal is null) return false;

            _resumeSignal = null;
            stateSession.Resumed();
        }

        resumeSignal.TrySetResult();

        return true;
    }

    public Task StopProcessing(CancellationToken cancellationToken) => processor.StopProcessing(cancellationToken);

    public ValueTask DisposeAsync() => processor.DisposeAsync();

    private Task Buffer(IReceivedMessage<TMessage> receivedMessage, CancellationToken transportToken, CancellationToken stoppingToken)
        => _buffer.Writer.TryWrite(receivedMessage) ? Task.CompletedTask : BufferWhenThereIsRoom(receivedMessage, transportToken, stoppingToken);

    private async Task BufferWhenThereIsRoom(IReceivedMessage<TMessage> receivedMessage, CancellationToken transportToken, CancellationToken stoppingToken)
    {
        using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, transportToken);

        try
        {
            await _buffer.Writer.WriteAsync(receivedMessage, writeCts.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private Task MarkHalted(Exception failure)
    {
        var resumeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_resumeLock)
        {
            _resumeSignal = resumeSignal;
            stateSession.Halted(failure);
        }

        return resumeSignal.Task;
    }

    private async Task HoldUntilResumed(Task resumed, CancellationToken stoppingToken)
    {
        try
        {
            await resumed.WaitAsync(stoppingToken);
        }
        finally
        {
            lock (_resumeLock)
            {
                if (_resumeSignal is not null && ReferenceEquals(_resumeSignal.Task, resumed))
                    _resumeSignal = null;
            }
        }
    }

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

    private async Task<Task?> ProcessBatch(IReadOnlyList<IReceivedMessage<TMessage>> batch, CancellationToken stoppingToken)
    {
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
                return null;
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

                var resumed = MarkHalted(e);
                handlerInstrumentation.RecordHalted(handlerName, queueName, e.Message);
                logger.LogCritical(e,
                    "{HandlerName} exhausted {MaxRetries} in-process retries on stream queue '{QueueName}' partition {Partition} — halting consumption. " +
                    "The failed batch is held and not acknowledged, the stream offset will not advance past it, and no further messages will be processed " +
                    "or dropped: the transport stops receiving and the broker keeps the rest of the stream. The partition stays halted until it is " +
                    "resumed (the held batch is then run again) or revoked, or the process stops (the batch is then replayed from the stored position).",
                    handlerName, maxRetries, queueName, partition);
                return resumed;
            }
        }
    }

    private async Task SaveCheckpointIfPresent(IReadOnlyList<IReceivedMessage<TMessage>> batch, CancellationToken cancellationToken)
    {
        var lastOffset = batch[^1].Properties.StreamOffset;
        if (lastOffset is null) return;

        await MessageStreamCheckpointHelper.SaveStreamPositionIfPresent<TMessage>(
            serviceScopeFactory, consumerName, partition, lastOffset.Value, handlerName, queueName, handlerInstrumentation, cancellationToken);
    }
}
