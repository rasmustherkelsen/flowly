using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.BackgroundServices;

internal abstract class ServiceBusMessageHandlerBackgroundServiceBase<TMessage> : BackgroundService where TMessage : class
{
    private readonly IServiceBusProcessor _processor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HandlerSettings<TMessage> _handlerSettings;
    private readonly ILogger _logger;

    public ServiceBusMessageHandlerBackgroundServiceBase(
        IServiceBusClient client,
        IServiceScopeFactory serviceScopeFactory,
        HandlerSettings<TMessage> handlerSettings,
        ILogger logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _handlerSettings = handlerSettings;
        _logger = logger;

        _processor = client.CreateProcessor(
            handlerSettings.QueueName,
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = handlerSettings.MaxConcurrentCalls,
                MaxAutoLockRenewalDuration = TimeSpan.FromHours(6),
                AutoCompleteMessages = true,
                ReceiveMode = handlerSettings.ReadAndDelete ? ServiceBusReceiveMode.ReceiveAndDelete : ServiceBusReceiveMode.PeekLock
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += HandleMessage;
        _processor.ProcessErrorAsync += ErrorHandlerAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation("{HandlerName} waiting for messages on queue '{QueueName}'", _handlerSettings.HandlerName, _handlerSettings.QueueName);
    }

    private async Task HandleMessage(ProcessMessageEventArgs processMessageEventArgs)
    {
        var body = processMessageEventArgs.Message.Body.ToString();
            var message = JsonSerializer.Deserialize<TMessage>(body)!;
        
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        await OnHandleMessage(message, processMessageEventArgs, scope.ServiceProvider);

        _logger.LogInformation("{HandlerName} handled as message", _handlerSettings.HandlerName);
    }

    protected internal abstract Task OnHandleMessage(TMessage message, ProcessMessageEventArgs processMessageEventArgs, IServiceProvider serviceProvider);

    private async Task ErrorHandlerAsync(ProcessErrorEventArgs processMessageEventArgs)
    {
        await using var serviceProviderScope = _serviceScopeFactory.CreateAsyncScope();
        await OnMessageHandlingError(_logger, serviceProviderScope.ServiceProvider, processMessageEventArgs);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync(cancellationToken);
        await _processor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }

    protected abstract Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ProcessErrorEventArgs processMessageEventArgs);
}