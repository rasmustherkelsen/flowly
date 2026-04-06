using System.Diagnostics;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

public abstract class ServiceBusMessageHandlerBackgroundServiceBase<TMessage> : BackgroundService where TMessage : class
{
    private readonly IMessageBusClient _messageBusClient;
    private IMessageBusProcessor<TMessage>? _messageBusProcessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HandlerSettings<TMessage> _handlerSettings;
    private readonly ILogger _logger;
    private readonly HandlerInstrumentation _handlerInstrumentation;

    public ServiceBusMessageHandlerBackgroundServiceBase(
        IMessageBusClient messageBusClient,
        IServiceScopeFactory serviceScopeFactory,
        HandlerSettings<TMessage> handlerSettings,
        ILogger logger,
        HandlerInstrumentation handlerInstrumentation)
    {
        _messageBusClient = messageBusClient;
        _serviceScopeFactory = serviceScopeFactory;
        _handlerSettings = handlerSettings;
        _logger = logger;
        _handlerInstrumentation = handlerInstrumentation;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _messageBusProcessor = await _messageBusClient.CreateProcessor<TMessage>(
            _handlerSettings.QueueName,
            new MessageBusProcessorOptions(_handlerSettings.MaxConcurrentCalls,
                _handlerSettings.ReadAndDelete
                    ? MessageBusReceiveMode.ReceiveAndDelete
                    : MessageBusReceiveMode.PeekLock));

        _messageBusProcessor.ProcessMessage += OnProcessMessage;
        _messageBusProcessor.ProcessError += OnProcessError;

        await _messageBusProcessor.StartProcessingMessages(stoppingToken);

        _logger.LogInformation("{HandlerName} waiting for messages on queue '{QueueName}'", _handlerSettings.HandlerName, _handlerSettings.QueueName);
    }

    private async Task OnProcessMessage(IReceivedMessage<TMessage> receivedMessage, CancellationToken cancellationToken)
    {
        _handlerInstrumentation.RecordReceived(_handlerSettings.HandlerName, _handlerSettings.QueueName);
        var sw = Stopwatch.StartNew();
        using var activity = _handlerInstrumentation.StartHandling(_handlerSettings.HandlerName, _handlerSettings.QueueName);

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
            _handlerInstrumentation.RecordSucceeded(_handlerSettings.HandlerName, _handlerSettings.QueueName, sw.Elapsed.TotalMilliseconds);
            _logger.LogInformation("{HandlerName} handled message", _handlerSettings.HandlerName);
            return;
        }

        var currentRetry = receivedMessage.Properties.RetryCount;
        if (currentRetry < _handlerSettings.MaxRetries)
        {
            await RepublishForRetry(receivedMessage, currentRetry + 1, cancellationToken);
            await receivedMessage.Complete(cancellationToken);
            _handlerInstrumentation.RecordRetried(_handlerSettings.HandlerName, _handlerSettings.QueueName);
            _logger.LogWarning("{HandlerName} message handling failed, retrying (attempt {Next}/{Max})",
                _handlerSettings.HandlerName, currentRetry + 1, _handlerSettings.MaxRetries);
            return;
        }

        _handlerInstrumentation.RecordFailed(_handlerSettings.HandlerName, _handlerSettings.QueueName);
        await OnRetriesExhausted(receivedMessage, handlingException, scope.ServiceProvider, cancellationToken);
    }

    private async Task RepublishForRetry(IReceivedMessage<TMessage> receivedMessage, int retryCount, CancellationToken cancellationToken)
    {
        var scheduledTime = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(_handlerSettings.RetryDelaySeconds);
        var props = receivedMessage.Properties with { RetryCount = retryCount, ScheduledEnqueueTime = scheduledTime };

        var sender = await _messageBusClient.CreateMessageBusSender(_handlerSettings.QueueName);
        await sender.SendMessage(receivedMessage.Body, props, cancellationToken);
    }

    private async Task OnProcessError(ErrorDetails errorDetails)
    {
        await using var serviceProviderScope = _serviceScopeFactory.CreateAsyncScope();
        await OnMessageHandlingError(_logger, serviceProviderScope.ServiceProvider, errorDetails);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_messageBusProcessor != null)
        {
            await _messageBusProcessor.StopProcessing(cancellationToken);
            await _messageBusProcessor.DisposeAsync();
        }
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
