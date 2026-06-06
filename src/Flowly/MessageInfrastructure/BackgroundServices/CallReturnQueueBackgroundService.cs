using Flowly.MessageInfrastructure.Callers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

internal sealed class CallReturnQueueBackgroundService<TReturn>(
    IMessageBusClientRegistry clientRegistry,
    CallReturnQueueSettings<TReturn> settings,
    IPendingCallRegistry<TReturn> pendingCallRegistry,
    ILogger<CallReturnQueueBackgroundService<TReturn>> logger) : BackgroundService
    where TReturn : class
{
    private IMessageBusProcessor<TReturn>? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = clientRegistry.GetClient(settings.ProviderName);

        _processor = await client.CreateProcessor<TReturn>(
            settings.ReturnQueueName,
            new MessageBusProcessorOptions(10, MessageBusReceiveMode.ReceiveAndDelete));

        _processor.ProcessMessage += OnMessage;
        _processor.ProcessError += OnError;

        await _processor.StartProcessingMessages(stoppingToken);

        logger.LogInformation("Listening for call responses on '{ReturnQueue}'", settings.ReturnQueueName);
    }

    private Task OnMessage(IReceivedMessage<TReturn> receivedMessage, CancellationToken cancellationToken)
    {
        var correlationId = receivedMessage.Properties.CorrelationId;

        if (!pendingCallRegistry.TryResolve(correlationId, receivedMessage.Body))
            logger.LogDebug("Received call response with CorrelationId '{CorrelationId}' but no pending call was found — caller may have timed out", correlationId);

        return Task.CompletedTask;
    }

    private Task OnError(ErrorDetails errorDetails)
    {
        logger.LogError(errorDetails.Exception, "Error processing call response on '{ReturnQueue}'", settings.ReturnQueueName);
        return Task.CompletedTask;
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
}
