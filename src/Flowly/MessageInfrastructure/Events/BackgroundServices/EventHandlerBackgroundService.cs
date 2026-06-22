using System.Diagnostics;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.Events.BackgroundServices;

internal class EventHandlerBackgroundService<TEvent, THandler>(
    IMessageBusClientRegistry clientRegistry,
    IServiceScopeFactory serviceScopeFactory,
    EventHandlerSettings<TEvent, THandler> settings,
    ILogger<EventHandlerBackgroundService<TEvent, THandler>> logger,
    IEventHandlerInstrumentation instrumentation)
    : BackgroundService
    where TEvent : class
    where THandler : EventHandlerBase<TEvent>
{
    private string _messagingSystem = string.Empty;
    private IMessageBusProcessor<TEvent>? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = clientRegistry.GetClient(settings.ProviderName);

        if (client is not IEventCapableMessageBusClient eventCapableClient)
            throw new InvalidOperationException(
                $"The message bus client for provider '{settings.ProviderName}' does not support events. " +
                $"The client must implement {nameof(IEventCapableMessageBusClient)}.");

        _messagingSystem = client.MessagingSystem;

        _processor = await eventCapableClient.CreateEventProcessor<TEvent>(
            settings.TopicName,
            settings.SubscriptionName,
            new MessageBusProcessorOptions(settings.MaxConcurrentCalls, MessageBusReceiveMode.PeekLock));

        _processor.ProcessMessage += OnProcessMessage;
        _processor.ProcessError += OnProcessError;

        await _processor.StartProcessingMessages(stoppingToken);

        logger.LogInformation(
            "{HandlerName} waiting for events on '{Topic}' (subscription: '{Subscription}')",
            settings.HandlerName, settings.TopicName, settings.SubscriptionName);
    }

    private async Task OnProcessMessage(IReceivedMessage<TEvent> receivedMessage, CancellationToken cancellationToken)
    {
        instrumentation.RecordReceived(settings.HandlerName, settings.TopicName);
        var sw = Stopwatch.StartNew();
        var parentContext = ParseParentContext(receivedMessage.Properties);
        using var activity = instrumentation.StartHandling(settings.HandlerName, settings.TopicName, _messagingSystem, receivedMessage.Properties, parentContext);

        if (IsBodyCorrupt(receivedMessage, out var deserializationException))
        {
            instrumentation.RecordFailed(settings.HandlerName, settings.TopicName);
            logger.LogError(deserializationException, "{HandlerName} event body deserialization failed, dead-lettering poison message", settings.HandlerName);
            await receivedMessage.DeadLetter($"Deserialization failed: {deserializationException!.Message}", cancellationToken);
            return;
        }

        activity.ApplyTagsFrom(receivedMessage.Body);

        await using var scope = serviceScopeFactory.CreateAsyncScope();

        Exception? handlingException = null;
        try
        {
            var handler = scope.ServiceProvider.GetRequiredService<THandler>();
            var context = new EventContext<TEvent>(
                receivedMessage.Body,
                receivedMessage.Properties.MessageId,
                receivedMessage.Properties.CorrelationId,
                DateTimeOffset.UtcNow);

            await handler.Handle(context, cancellationToken);
        }
        catch (Exception ex)
        {
            handlingException = ex;
        }

        if (handlingException == null)
        {
            instrumentation.RecordSucceeded(settings.HandlerName, settings.TopicName, sw.Elapsed.TotalMilliseconds);
            logger.LogInformation("{HandlerName} handled event", settings.HandlerName);
            await receivedMessage.Complete(cancellationToken);
            return;
        }

        var currentRetry = receivedMessage.Properties.RetryCount;
        if (currentRetry < settings.MaxRetries)
        {
            await RepublishForRetry(receivedMessage, currentRetry + 1, cancellationToken);
            instrumentation.RecordRetried(settings.HandlerName, settings.TopicName);
            logger.LogWarning("{HandlerName} event handling failed, retrying (attempt {Next}/{Max})", settings.HandlerName, currentRetry + 1, settings.MaxRetries);
            await receivedMessage.Complete(cancellationToken);
            return;
        }

        instrumentation.RecordFailed(settings.HandlerName, settings.TopicName);
        await receivedMessage.DeadLetter(handlingException.Message, cancellationToken);
        logger.LogError(handlingException, "{HandlerName} event handling failed after {MaxRetries} retries, dead-lettered", settings.HandlerName, settings.MaxRetries);
    }

    private async Task RepublishForRetry(IReceivedMessage<TEvent> receivedMessage, int retryCount, CancellationToken cancellationToken)
    {
        var client = clientRegistry.GetClient(settings.ProviderName);
        var eventCapableClient = (IEventCapableMessageBusClient)client;
        var scheduledTime = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(settings.RetryDelaySeconds);
        var props = receivedMessage.Properties with { RetryCount = retryCount, ScheduledEnqueueTime = scheduledTime };
        var sender = await eventCapableClient.CreateEventRetrySender(settings.TopicName, settings.SubscriptionName);
        await sender.SendMessage(receivedMessage.Body, props, cancellationToken);
    }

    private async Task OnProcessError(ErrorDetails errorDetails)
    {
        logger.LogError(errorDetails.Exception, "{HandlerName} event processor error", settings.HandlerName);
        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            await _processor.StopProcessing(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private static bool IsBodyCorrupt(IReceivedMessage<TEvent> receivedMessage, out Exception? exception)
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