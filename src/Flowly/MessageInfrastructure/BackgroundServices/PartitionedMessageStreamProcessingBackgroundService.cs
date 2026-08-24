using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

/// <summary>
///     Supervises one <see cref="StreamPartitionRunner{TMessage,THandler}" /> per partition currently owned by this
///     process, starting and stopping them as <see cref="IPartitionedStreamConsumer{TMessage}" /> reports ownership
///     changes. A halted partition (retries exhausted) only stops that partition — the others, and the service
///     itself, keep running, since partitions are independent logs.
/// </summary>
internal sealed class PartitionedMessageStreamProcessingBackgroundService<TMessage, THandler>(
    IMessageBusClientRegistry clientRegistry,
    PartitionedMessageStreamHandlerSettings<TMessage, THandler> handlerSettings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PartitionedMessageStreamProcessingBackgroundService<TMessage, THandler>> logger,
    IHandlerInstrumentation handlerInstrumentation) : MessageProcessingBackgroundServiceBase<TMessage>
    where TMessage : class
    where THandler : MessageStreamHandler<TMessage>
{
    private readonly Dictionary<int, ActiveRunner> _activeRunners = new();
    private readonly HashSet<int> _initializedPartitions = new();
    private readonly Lock _lock = new();
    private IPartitionedStreamConsumer<TMessage>? _consumer;
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        var client = clientRegistry.GetClient(handlerSettings.ProviderName);

        if (client is not IPartitionedStreamCapableMessageBusClient partitionedClient)
            throw new InvalidOperationException(
                $"The message bus client for provider '{handlerSettings.ProviderName}' does not support partitioned message streaming. " +
                $"The client must implement {nameof(IPartitionedStreamCapableMessageBusClient)}.");

        var messagingSystem = client.MessagingSystem;

        _consumer = await partitionedClient.CreatePartitionedStreamConsumer<TMessage>(
            handlerSettings.QueueName,
            handlerSettings.PartitionCount,
            (partition, ct) => MessageStreamCheckpointHelper.ResolveStartPosition<TMessage>(
                serviceScopeFactory, handlerSettings.ConsumerName, partition, handlerSettings.StartPosition, ct,
                initializeCheckpoint: IsFirstAssignment(partition)),
            new MessageBusProcessorOptions(handlerSettings.MaxMessagesBeforeProcessing, MessageBusReceiveMode.PeekLock),
            logger);

        _consumer.PartitionAssigned += (partition, processor) =>
        {
            StartPartitionLoop(partition, processor, messagingSystem);
            return Task.CompletedTask;
        };

        _consumer.PartitionRevoked += StopPartitionLoop;

        await _consumer.StartProcessingMessages(stoppingToken);
        logger.LogInformation("{HandlerName} waiting for partitioned stream messages on queue '{QueueName}' ({PartitionCount} partitions)",
            handlerSettings.HandlerName, handlerSettings.QueueName, handlerSettings.PartitionCount);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private bool IsFirstAssignment(int partition)
    {
        lock (_lock)
        {
            return _initializedPartitions.Add(partition);
        }
    }

    private void StartPartitionLoop(int partition, IMessageBusProcessor<TMessage> processor, string messagingSystem)
    {
        var runner = new StreamPartitionRunner<TMessage, THandler>(
            processor,
            partition,
            handlerSettings.HandlerName,
            handlerSettings.QueueName,
            handlerSettings.ConsumerName,
            handlerSettings.MaxMessagesBeforeProcessing,
            handlerSettings.MaxWaitTime,
            handlerSettings.MaxRetries,
            handlerSettings.RetryDelaySeconds,
            messagingSystem,
            serviceScopeFactory,
            logger,
            handlerInstrumentation);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken);
        var runTask = runner.Run(cts.Token);
        var activeRunner = new ActiveRunner(runner, runTask, cts);

        lock (_lock)
        {
            _activeRunners[partition] = activeRunner;
        }

        _ = runTask.ContinueWith(_ =>
        {
            var stillCurrent = false;
            lock (_lock)
            {
                if (_activeRunners.TryGetValue(partition, out var current) && ReferenceEquals(current, activeRunner))
                {
                    _activeRunners.Remove(partition);
                    stillCurrent = true;
                }
            }

            // If the partition was revoked and reassigned before this continuation ran, `activeRunner` has already
            // been replaced in the dictionary by a newer instance — skip removal/disposal so we don't tear down
            // the currently-active runner for this partition (an ABA race between revoke and reassignment).
            if (stillCurrent)
                _ = DisposeEntry(activeRunner);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task StopPartitionLoop(int partition)
    {
        ActiveRunner? entry;
        lock (_lock)
        {
            _activeRunners.Remove(partition, out entry);
        }

        if (entry is null) return;

        await entry.Cts.CancelAsync();
        try
        {
            await entry.RunTask;
        }
        catch (OperationCanceledException)
        {
        }

        await DisposeEntry(entry);
    }

    private static async Task DisposeEntry(ActiveRunner entry)
    {
        await entry.Runner.DisposeAsync();
        entry.Cts.Dispose();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        List<ActiveRunner> entries;
        lock (_lock)
        {
            entries = [.._activeRunners.Values];
            _activeRunners.Clear();
        }

        foreach (var entry in entries)
        {
            await entry.Runner.StopProcessing(cancellationToken);
            await entry.Cts.CancelAsync();

            try
            {
                await entry.RunTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            await DisposeEntry(entry);
        }

        if (_consumer != null)
        {
            await _consumer.StopProcessing(cancellationToken);
            await _consumer.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private sealed record ActiveRunner(StreamPartitionRunner<TMessage, THandler> Runner, Task RunTask, CancellationTokenSource Cts);
}
