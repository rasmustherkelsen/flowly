using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

/// <summary>
///     Supervises one <see cref="StreamPartitionRunner{TMessage,THandler}" /> per partition currently owned by this
///     process, starting and stopping them as <see cref="IPartitionedStreamConsumer{TMessage}" /> reports ownership
///     changes. A halted partition (retries exhausted) only holds that partition — its runner keeps the failed batch and
///     applies backpressure until it is resumed or revoked, while the others, and the service itself, keep running, since
///     partitions are independent logs. A runner is always cancelled before its processor is stopped, so a runner held by
///     backpressure cannot keep the transport from stopping. Records the state of every partition this process
///     owns (running, waiting for an unresolved start position, halted) into the <see cref="IStreamPartitionStateRegistry" />;
///     the transport only reports assignment and revocation.
/// </summary>
internal sealed class PartitionedMessageStreamProcessingBackgroundService<TMessage, THandler>(
    IMessageBusClientRegistry clientRegistry,
    PartitionedMessageStreamHandlerSettings<TMessage, THandler> handlerSettings,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PartitionedMessageStreamProcessingBackgroundService<TMessage, THandler>> logger,
    IHandlerInstrumentation handlerInstrumentation,
    IStreamPartitionStateRegistry stateRegistry,
    IStreamPartitionResumeRegistry resumeRegistry) : MessageProcessingBackgroundServiceBase<TMessage>
    where TMessage : class
    where THandler : MessageStreamHandler<TMessage>
{
    private readonly StreamPartitionStateReporter _stateReporter = new(
        stateRegistry, handlerInstrumentation, handlerSettings.HandlerName, handlerSettings.QueueName, handlerSettings.ConsumerName);

    private readonly Dictionary<int, ActiveRunner> _activeRunners = new();
    private readonly HashSet<int> _initializedPartitions = new();
    private readonly Lock _lock = new();
    private IPartitionedStreamConsumer<TMessage>? _consumer;
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        var partitionedClient = clientRegistry.GetClient<IPartitionedStreamCapableMessageBusClient>(handlerSettings.ProviderName);

        var transportClient = clientRegistry.GetTransportClient(handlerSettings.ProviderName);
        var messagingSystem = transportClient.MessagingSystem;
        var startOffsetResolver = transportClient as IStreamStartOffsetResolver;
        MessageStreamCheckpointHelper.ThrowIfCheckpointRegisteredForInMemory<TMessage>(serviceScopeFactory, messagingSystem);

        _consumer = await partitionedClient.CreatePartitionedStreamConsumer<TMessage>(
            handlerSettings.QueueName,
            handlerSettings.PartitionCount,
            (partition, ct) => ResolveStartPosition(partition, startOffsetResolver, ct),
            new MessageBusProcessorOptions(handlerSettings.MaxMessagesBeforeProcessing, MessageBusReceiveMode.PeekLock)
            {
                ConsumerName = handlerSettings.ConsumerName
            },
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

    private async Task<StartPosition> ResolveStartPosition(int partition, IStreamStartOffsetResolver? startOffsetResolver, CancellationToken cancellationToken)
    {
        try
        {
            return await MessageStreamCheckpointHelper.ResolveStartPosition<TMessage>(
                serviceScopeFactory, handlerSettings.ConsumerName, partition, handlerSettings.StartPosition,
                handlerSettings.HandlerName, handlerSettings.QueueName, handlerInstrumentation, logger, cancellationToken,
                initializeCheckpoint: !IsCheckpointInitialized(partition), startOffsetResolver: startOffsetResolver,
                checkpointInitialized: () => MarkCheckpointInitialized(partition));
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (!HasActiveRunner(partition))
                _stateReporter.Waiting(partition, exception);

            throw;
        }
    }

    private bool HasActiveRunner(int partition)
    {
        lock (_lock)
        {
            return _activeRunners.ContainsKey(partition);
        }
    }

    private bool IsCheckpointInitialized(int partition)
    {
        lock (_lock)
        {
            return _initializedPartitions.Contains(partition);
        }
    }

    private void MarkCheckpointInitialized(int partition)
    {
        lock (_lock)
        {
            _initializedPartitions.Add(partition);
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
            handlerInstrumentation,
            _stateReporter.Running(partition),
            resumeRegistry);

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

        _stateReporter.Revoked(partition);

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
        try
        {
            await StopRunnersAndConsumer(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
        finally
        {
            _stateReporter.Stopped();
        }
    }

    private async Task StopRunnersAndConsumer(CancellationToken cancellationToken)
    {
        List<ActiveRunner> entries;
        lock (_lock)
        {
            entries = [.._activeRunners.Values];
            _activeRunners.Clear();
        }

        foreach (var entry in entries)
        {
            await entry.Cts.CancelAsync();

            try
            {
                await entry.RunTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            await entry.Runner.StopProcessing(cancellationToken);
            await DisposeEntry(entry);
        }

        if (_consumer != null)
        {
            await _consumer.StopProcessing(cancellationToken);
            await _consumer.DisposeAsync();
        }
    }

    private sealed record ActiveRunner(StreamPartitionRunner<TMessage, THandler> Runner, Task RunTask, CancellationTokenSource Cts);
}
