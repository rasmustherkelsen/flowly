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
    IStreamPartitionStateRegistry stateRegistry) : MessageProcessingBackgroundServiceBase<TMessage>
    where TMessage : class
    where THandler : MessageStreamHandler<TMessage>
{
    private readonly StreamPartitionStateReporter _stateReporter = new(
        stateRegistry, handlerInstrumentation, handlerSettings.HandlerName, handlerSettings.QueueName, handlerSettings.ConsumerName);

    private StreamPartitionRunner<TMessage, THandler>? _runner;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var streamCapableClient = clientRegistry.GetClient<IStreamCapableMessageBusClient>(handlerSettings.ProviderName);

        var messagingSystem = streamCapableClient.MessagingSystem;
        MessageStreamCheckpointHelper.ThrowIfCheckpointRegisteredForInMemory<TMessage>(serviceScopeFactory, messagingSystem);

        var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TMessage>(
            serviceScopeFactory, handlerSettings.ConsumerName, null, handlerSettings.StartPosition,
            handlerSettings.HandlerName, handlerSettings.QueueName, handlerInstrumentation, logger, stoppingToken,
            startOffsetResolver: streamCapableClient as IStreamStartOffsetResolver);

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
            _stateReporter.Running(null));

        await _runner.Run(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_runner != null)
            {
                await _runner.StopProcessing(cancellationToken);
                await _runner.DisposeAsync();
            }

            await base.StopAsync(cancellationToken);
        }
        finally
        {
            _stateReporter.Stopped();
        }
    }
}
