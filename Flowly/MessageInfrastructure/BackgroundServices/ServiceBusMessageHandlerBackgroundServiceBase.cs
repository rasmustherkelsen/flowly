using Flowly.MessageInfrastructure.Model;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

public abstract class ServiceBusMessageHandlerBackgroundServiceBase<TMessage> : BackgroundService where TMessage : class
{
    private readonly IMessageBusClient _messageBusClient;
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
        _messageBusClient = messageBusClient;
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

        Exception? handlingException = null;
        try
        {
            await OnHandleMessage(receivedMessage, scope.ServiceProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            handlingException = ex;
        }

        if (handlingException == null)
        {
            await receivedMessage.Complete(cancellationToken);
            _logger.LogInformation("{HandlerName} handled message", _handlerSettings.HandlerName);
            return;
        }

        var currentRetry = receivedMessage.Properties.RetryCount;
        if (currentRetry < _handlerSettings.MaxRetries)
        {
            await RepublishForRetry(receivedMessage, currentRetry + 1, cancellationToken);
            await receivedMessage.Complete(cancellationToken);
            _logger.LogWarning("{HandlerName} message handling failed, retrying (attempt {Next}/{Max})",
                _handlerSettings.HandlerName, currentRetry + 1, _handlerSettings.MaxRetries);
            return;
        }

        await OnRetriesExhausted(receivedMessage, handlingException, scope.ServiceProvider, cancellationToken);
    }

    private async Task RepublishForRetry(IReceivedMessage<TMessage> receivedMessage, int retryCount, CancellationToken cancellationToken)
    {
        var scheduledTime = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(_handlerSettings.RetryDelaySeconds);
        var props = receivedMessage.Properties with { RetryCount = retryCount, ScheduledEnqueueTime = scheduledTime };

        var sender = _messageBusClient.CreateMessageBusSender(_handlerSettings.QueueName);
        await sender.SendMessage(receivedMessage.Body, props, cancellationToken);
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

    protected virtual async Task OnRetriesExhausted(IReceivedMessage<TMessage> receivedMessage, Exception exception, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await receivedMessage.DeadLetter(exception.Message, cancellationToken);
        _logger.LogError(exception, "{HandlerName} message handling failed after {MaxRetries} retries, dead-lettered", _handlerSettings.HandlerName, _handlerSettings.MaxRetries);
    }

    protected abstract Task OnHandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    protected abstract Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails);
}
