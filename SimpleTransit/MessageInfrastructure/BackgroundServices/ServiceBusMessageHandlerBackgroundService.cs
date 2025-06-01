using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.AzureServiceBusWrappers;
using Microsoft.Extensions.Logging;

namespace SimpleTransit.MessageInfrastructure.BackgroundServices;

internal class ServiceBusMessageHandlerBackgroundService<TMessage> : ServiceBusMessageHandlerBackgroundServiceBase<TMessage> where TMessage : class
{
    public ServiceBusMessageHandlerBackgroundService(
        IServiceBusClient client, 
        IServiceScopeFactory serviceScopeFactory, 
        HandlerSettings<TMessage> handlerSettings, 
        ILogger<ServiceBusMessageHandlerBackgroundService<TMessage>> logger) : base(client, serviceScopeFactory, handlerSettings, logger)
    {
    }

    protected internal override async Task OnHandleMessage(TMessage message, ProcessMessageEventArgs processMessageEventArgs, IServiceProvider serviceProvider)
    {
        var handler = serviceProvider.GetRequiredService<IMessageHandler<TMessage>>();
        await handler.Handle(new MessageContext<TMessage>(message, processMessageEventArgs.CancellationToken));
    }
    
    protected override Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ProcessErrorEventArgs processMessageEventArgs)
    {
        logger.LogError(processMessageEventArgs.Exception.Message);
        return Task.CompletedTask;
    }
}