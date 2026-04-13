using System.Diagnostics;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

public abstract class ServiceBusMessageHandlerBackgroundServiceBase<TMessage> : BackgroundService where TMessage : class
{
    private readonly IMessageBusClientRegistry _clientRegistry;
    private IMessageBusProcessor<TMessage>? _messageBusProcessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HandlerSettings<TMessage> _handlerSettings;
    private readonly ILogger _logger;
    private readonly HandlerInstrumentation _handlerInstrumentation;
    private string _messagingSystem = string.Empty;

    public ServiceBusMessageHandlerBackgroundServiceBase(
        IMessageBusClientRegistry clientRegistry,
        IServiceScopeFactory serviceScopeFactory,
        HandlerSettings<TMessage> handlerSettings,
        ILogger logger,
        HandlerInstrumentation handlerInstrumentation)
    {
        _clientRegistry = clientRegistry;
        _serviceScopeFactory = serviceScopeFactory;
        _handlerSettings = handlerSettings;
        _logger = logger;
        _handlerInstrumentation = handlerInstrumentation;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = _clientRegistry.GetClient(_handlerSettings.ProviderName);
        _messagingSystem = client.MessagingSystem;

        _messageBusProcessor = await client.CreateProcessor<TMessage>(
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
        var parentContext = ParseParentContext(receivedMessage.Properties);
        using var activity = _handlerInstrumentation.StartHandling(_handlerSettings.HandlerName, _handlerSettings.QueueName, _messagingSystem, receivedMessage.Properties, parentContext);

        if (IsBodyCorrupt(receivedMessage, out var deserializationException))
        {
            _handlerInstrumentation.RecordFailed(_handlerSettings.HandlerName, _handlerSettings.QueueName);
            _logger.LogError(deserializationException, "{HandlerName} message body deserialization failed, dead-lettering poison message", _handlerSettings.HandlerName);
            await receivedMessage.DeadLetter($"Deserialization failed: {deserializationException!.Message}", cancellationToken);
            return;
        }

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
            _handlerInstrumentation.RecordSucceeded(_handlerSettings.HandlerName, _handlerSettings.QueueName, sw.Elapsed.TotalMilliseconds);
            _logger.LogInformation("{HandlerName} handled message", _handlerSettings.HandlerName);
            if (!_handlerSettings.ReadAndDelete)
                await receivedMessage.Complete(cancellationToken);
            return;
        }

        var currentRetry = receivedMessage.Properties.RetryCount;
        if (currentRetry < _handlerSettings.MaxRetries)
        {
            await RepublishForRetry(receivedMessage, currentRetry + 1, cancellationToken);
            _handlerInstrumentation.RecordRetried(_handlerSettings.HandlerName, _handlerSettings.QueueName);
            _logger.LogWarning("{HandlerName} message handling failed, retrying (attempt {Next}/{Max})",
                _handlerSettings.HandlerName, currentRetry + 1, _handlerSettings.MaxRetries);
            if (!_handlerSettings.ReadAndDelete)
                await receivedMessage.Complete(cancellationToken);
            return;
        }

        _handlerInstrumentation.RecordFailed(_handlerSettings.HandlerName, _handlerSettings.QueueName);
        await OnRetriesExhausted(receivedMessage, handlingException, scope.ServiceProvider, cancellationToken);
    }

    private async Task RepublishForRetry(IReceivedMessage<TMessage> receivedMessage, int retryCount, CancellationToken cancellationToken)
    {
        var scheduledTime = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(_handlerSettings.RetryDelaySeconds);
        var props = receivedMessage.Properties with { RetryCount = retryCount, ScheduledEnqueueTime = scheduledTime };

        var client = _clientRegistry.GetClient(_handlerSettings.ProviderName);
        var sender = await client.CreateMessageBusSender(_handlerSettings.QueueName);
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

    private static bool IsBodyCorrupt(IReceivedMessage<TMessage> receivedMessage, out Exception? exception)
    {
        try
        {
            _ = receivedMessage.Body;
            exception = null;
            return false;
        }
        catch (Exception ex)
        {
            exception = ex;
            return true;
        }
    }

    private static ActivityContext ParseParentContext(MessageProperties properties)
    {
        if (properties.Traceparent is null) return default;

        return ActivityContext.TryParse(properties.Traceparent, properties.Tracestate, isRemote: true, out var context)
            ? context
            : default;
    }

    protected abstract Task OnHandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    protected abstract Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails);
}
