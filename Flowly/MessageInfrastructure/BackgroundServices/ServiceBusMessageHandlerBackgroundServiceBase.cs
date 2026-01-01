using Flowly.MessageInfrastructure.Model;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

internal abstract class ServiceBusMessageHandlerBackgroundServiceBase<TMessage> : BackgroundService where TMessage : class
{
    private readonly IMessageBusProcessor<TMessage> _messageBusProcessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HandlerSettings<TMessage> _handlerSettings;
    private readonly ILogger _logger;

    public ServiceBusMessageHandlerBackgroundServiceBase(
        IMessageBusClient messageBusClient,
        IServiceScopeFactory serviceScopeFactory,
        HandlerSettings<TMessage> handlerSettings,
        ILogger logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _handlerSettings = handlerSettings;
        _logger = logger;

        _messageBusProcessor = messageBusClient.CreateProcessor<TMessage>(
            handlerSettings.QueueName,
            new MessageBusProcessorOptions(handlerSettings.MaxConcurrentCalls,
                handlerSettings.ReadAndDelete
                    ? MessageBusReceiveMode.ReceiveAndDelete
                    : MessageBusReceiveMode.PeekLock));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _messageBusProcessor.ProcessMessage += OnProcessMessage;
        _messageBusProcessor.ProcessError += OnProcessError;

        await _messageBusProcessor.StartProcessingMessages(stoppingToken);

        _logger.LogInformation("{HandlerName} waiting for messages on queue '{QueueName}'", _handlerSettings.HandlerName, _handlerSettings.QueueName);
    }

    private async Task OnProcessMessage(IReceivedMessage<TMessage> receivedMessage, CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        await OnHandleMessage(receivedMessage, scope.ServiceProvider, cancellationToken);

        _logger.LogInformation("{HandlerName} handled as message", _handlerSettings.HandlerName);
    }

    private async Task OnProcessError(ErrorDetails errorDetails)
    {
        await using var serviceProviderScope = _serviceScopeFactory.CreateAsyncScope();
        await OnMessageHandlingError(_logger, serviceProviderScope.ServiceProvider, errorDetails);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _messageBusProcessor.StopProcessing(cancellationToken);
        await _messageBusProcessor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }

    protected abstract Task OnHandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken);
    
    protected abstract Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails);
}