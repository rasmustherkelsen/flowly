using Microsoft.Extensions.DependencyInjection;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using Microsoft.Extensions.Logging;
using SimpleTransit.MessagingAbstractions;

namespace SimpleTransit.MessageInfrastructure.BackgroundServices;

internal class ServiceBusMessageHandlerBackgroundService<TMessage> : ServiceBusMessageHandlerBackgroundServiceBase<TMessage> where TMessage : class
{
    public ServiceBusMessageHandlerBackgroundService(
        IMessageBusClient messageBusClient, 
        IServiceScopeFactory serviceScopeFactory, 
        HandlerSettings<TMessage> handlerSettings, 
        ILogger<ServiceBusMessageHandlerBackgroundService<TMessage>> logger) : base(messageBusClient, serviceScopeFactory, handlerSettings, logger)
    {
    }

    protected override async Task OnHandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetRequiredService<IMessageHandler<TMessage>>();
        await handler.Handle(new MessageContext<TMessage>(receivedMessage.Body, cancellationToken));
    }

    protected override Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails)
    {
        logger.LogError(errorDetails.Exception.Message);
        return Task.CompletedTask;
    }
}