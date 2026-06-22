using System.Diagnostics;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

/// <summary>
///     Hosted service that drives the message processing pipeline for a single queue and message type. At startup it
///     creates an <see cref="IMessageBusProcessor{TMessage}" /> via
///     <see cref="IMessageBusClient.CreateProcessor{TMessage}" />,
///     subscribes to its events, and starts processing. On shutdown it gracefully stops and disposes the processor.
///     One instance is registered per <see cref="MessageHandler{TMessage}" /> registration.
/// </summary>
/// <typeparam name="TMessage">The message type this service processes.</typeparam>
internal sealed class MessageProcessingBackgroundService<TMessage>(
    IMessageBusClientRegistry clientRegistry,
    IServiceScopeFactory serviceScopeFactory,
    IHandlerSettings<TMessage> handlerSettings,
    ILogger<MessageProcessingBackgroundService<TMessage>> logger,
    IHandlerInstrumentation handlerInstrumentation,
    IMessageHandlingStrategy<TMessage> strategy) : MessageProcessingBackgroundServiceBase<TMessage>
    where TMessage : class
{
    private readonly ILogger _logger = logger;
    private IMessageBusProcessor<TMessage>? _messageBusProcessor;
    private string _messagingSystem = string.Empty;

    /// <summary>
    ///     Creates the <see cref="IMessageBusProcessor{TMessage}" /> for the registered queue, wires up the
    ///     <see cref="IMessageBusProcessor{TMessage}.ProcessMessage" /> and
    ///     <see cref="IMessageBusProcessor{TMessage}.ProcessError" /> events, and starts processing. Runs until
    ///     <paramref name="stoppingToken" /> is cancelled.
    /// </summary>
    /// <param name="stoppingToken">Triggered when the hosted service is stopping.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = clientRegistry.GetClient(handlerSettings.ProviderName);
        _messagingSystem = client.MessagingSystem;

        _messageBusProcessor = await client.CreateProcessor<TMessage>(
            handlerSettings.QueueName,
            new MessageBusProcessorOptions(handlerSettings.MaxConcurrentCalls,
                handlerSettings.ReadAndDelete
                    ? MessageBusReceiveMode.ReceiveAndDelete
                    : MessageBusReceiveMode.PeekLock));

        _messageBusProcessor.ProcessMessage += OnProcessMessage;
        _messageBusProcessor.ProcessError += OnProcessError;

        await _messageBusProcessor.StartProcessingMessages(stoppingToken);

        _logger.LogInformation("{HandlerName} waiting for messages on queue '{QueueName}'", handlerSettings.HandlerName, handlerSettings.QueueName);
    }

    private async Task OnProcessMessage(IReceivedMessage<TMessage> receivedMessage, CancellationToken cancellationToken)
    {
        handlerInstrumentation.RecordReceived(handlerSettings.HandlerName, handlerSettings.QueueName);
        var sw = Stopwatch.StartNew();
        var parentContext = ParseParentContext(receivedMessage.Properties);
        using var activity = handlerInstrumentation.StartHandling(handlerSettings.HandlerName, handlerSettings.QueueName, _messagingSystem, receivedMessage.Properties, parentContext);

        if (IsBodyCorrupt(receivedMessage, out var deserializationException))
        {
            handlerInstrumentation.RecordFailed(handlerSettings.HandlerName, handlerSettings.QueueName);
            _logger.LogError(deserializationException, "{HandlerName} message body deserialization failed, dead-lettering poison message", handlerSettings.HandlerName);
            await receivedMessage.DeadLetter($"Deserialization failed: {deserializationException!.Message}", cancellationToken);
            return;
        }

        activity.ApplyTagsFrom(receivedMessage.Body);

        await using var scope = serviceScopeFactory.CreateAsyncScope();

        Exception? handlingException = null;
        try
        {
            await strategy.HandleMessage(receivedMessage, scope.ServiceProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            handlingException = ex;
        }

        if (handlingException == null)
        {
            handlerInstrumentation.RecordSucceeded(handlerSettings.HandlerName, handlerSettings.QueueName, sw.Elapsed.TotalMilliseconds);
            _logger.LogInformation("{HandlerName} handled message", handlerSettings.HandlerName);
            if (!handlerSettings.ReadAndDelete)
                await receivedMessage.Complete(cancellationToken);
            return;
        }

        var currentRetry = receivedMessage.Properties.RetryCount;
        if (currentRetry < handlerSettings.MaxRetries)
        {
            await RepublishForRetry(receivedMessage, currentRetry + 1, cancellationToken);
            handlerInstrumentation.RecordRetried(handlerSettings.HandlerName, handlerSettings.QueueName);
            _logger.LogWarning("{HandlerName} message handling failed, retrying (attempt {Next}/{Max})",
                handlerSettings.HandlerName, currentRetry + 1, handlerSettings.MaxRetries);
            if (!handlerSettings.ReadAndDelete)
                await receivedMessage.Complete(cancellationToken);
            return;
        }

        handlerInstrumentation.RecordFailed(handlerSettings.HandlerName, handlerSettings.QueueName);
        await strategy.OnRetriesExhausted(receivedMessage, handlingException, scope.ServiceProvider, cancellationToken);
        _logger.LogError(handlingException, "{HandlerName} message handling failed after {MaxRetries} retries", handlerSettings.HandlerName, handlerSettings.MaxRetries);
    }

    private async Task RepublishForRetry(IReceivedMessage<TMessage> receivedMessage, int retryCount, CancellationToken cancellationToken)
    {
        var scheduledTime = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(handlerSettings.RetryDelaySeconds);
        var props = receivedMessage.Properties with { RetryCount = retryCount, ScheduledEnqueueTime = scheduledTime };

        var client = clientRegistry.GetClient(handlerSettings.ProviderName);
        var sender = await client.CreateMessageBusSender(handlerSettings.QueueName);
        await sender.SendMessage(receivedMessage.Body, props, cancellationToken);
    }

    private async Task OnProcessError(ErrorDetails errorDetails)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        await strategy.OnMessageHandlingError(_logger, scope.ServiceProvider, errorDetails);
    }

    /// <summary>
    ///     Gracefully stops and disposes the underlying <see cref="IMessageBusProcessor{TMessage}" /> before
    ///     delegating to the base <see cref="BackgroundService.StopAsync" />.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that limits the time allowed for graceful shutdown.</param>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_messageBusProcessor != null)
        {
            await _messageBusProcessor.StopProcessing(cancellationToken);
            await _messageBusProcessor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
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

        return ActivityContext.TryParse(properties.Traceparent, properties.Tracestate, true, out var context)
            ? context
            : default;
    }
}