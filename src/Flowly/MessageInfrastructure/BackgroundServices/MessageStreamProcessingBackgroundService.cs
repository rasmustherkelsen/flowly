using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.StreamPartitions;
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
    IHandlerInstrumentation handlerInstrumentation,
    IStreamPartitionStateRegistry stateRegistry,
    IStreamPartitionResumeRegistry resumeRegistry,
    TimeProvider? timeProvider = null) : MessageProcessingBackgroundServiceBase<TMessage>
    where TMessage : class
    where THandler : MessageStreamHandler<TMessage>
{
    private static readonly TimeSpan InitialStartPositionRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxStartPositionRetryDelay = TimeSpan.FromSeconds(30);

    private readonly StreamPartitionStateReporter _stateReporter = new(
        stateRegistry, handlerInstrumentation, handlerSettings.HandlerName, handlerSettings.QueueName, handlerSettings.ConsumerName);

    private StreamPartitionRunner<TMessage, THandler>? _runner;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var streamCapableClient = clientRegistry.GetClient<IStreamCapableMessageBusClient>(handlerSettings.ProviderName);

        var messagingSystem = streamCapableClient.MessagingSystem;
        MessageStreamCheckpointHelper.ThrowIfCheckpointRegisteredForInMemory<TMessage>(serviceScopeFactory, messagingSystem);

        var resolvedStartPosition = await ResolveStartPositionUntilAvailable(streamCapableClient as IStreamStartOffsetResolver, stoppingToken);

        if (resolvedStartPosition is not { } startPosition)
        {
            return;
        }

        var processor = await streamCapableClient.CreateStreamProcessor<TMessage>(
            handlerSettings.QueueName,
            startPosition,
            new MessageBusProcessorOptions(handlerSettings.MaxMessagesBeforeProcessing, MessageBusReceiveMode.PeekLock)
            {
                ConsumerName = handlerSettings.ConsumerName
            });

        _runner = new StreamPartitionRunner<TMessage, THandler>(
            processor,
            null,
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
            _stateReporter.Running(null),
            resumeRegistry);

        await _runner.Run(stoppingToken);
    }

    private async Task<StartPosition?> ResolveStartPositionUntilAvailable(IStreamStartOffsetResolver? startOffsetResolver, CancellationToken stoppingToken)
    {
        var delay = InitialStartPositionRetryDelay;

        try
        {
            while (true)
            {
                try
                {
                    return await MessageStreamCheckpointHelper.ResolveStartPosition<TMessage>(
                        serviceScopeFactory, handlerSettings.ConsumerName, null, handlerSettings.StartPosition,
                        handlerSettings.HandlerName, handlerSettings.QueueName, handlerInstrumentation, logger, stoppingToken,
                        startOffsetResolver: startOffsetResolver);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _stateReporter.Waiting(null, exception);
                    logger.LogWarning(exception,
                        "{HandlerName} could not resolve the start position of stream queue '{QueueName}', retrying in {RetryDelay}",
                        handlerSettings.HandlerName, handlerSettings.QueueName, delay);
                }

                await Task.Delay(delay, timeProvider ?? TimeProvider.System, stoppingToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxStartPositionRetryDelay.Ticks));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await base.StopAsync(cancellationToken);

            if (_runner != null)
            {
                await _runner.StopProcessing(cancellationToken);
                await _runner.DisposeAsync();
            }
        }
        finally
        {
            _stateReporter.Stopped();
        }
    }
}
