using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.BackgroundServices;

internal class ServiceBusMessageHandlerBackgroundService<TMessage> : ServiceBusMessageHandlerBackgroundServiceBase<TMessage> where TMessage : class
{
    public ServiceBusMessageHandlerBackgroundService(
        IMessageBusClient messageBusClient,
        IServiceScopeFactory serviceScopeFactory,
        HandlerSettings<TMessage> handlerSettings,
        ILogger<ServiceBusMessageHandlerBackgroundService<TMessage>> logger,
        HandlerInstrumentation handlerInstrumentation) : base(messageBusClient, serviceScopeFactory, handlerSettings, logger, handlerInstrumentation)
    {
    }

    protected override async Task OnHandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetRequiredService<MessageHandlerBase<TMessage>>();
        await handler.Handle(new MessageContext<TMessage>(receivedMessage.Body, cancellationToken));
    }

    protected override Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails)
    {
        logger.LogError(errorDetails.Exception.Message);
        return Task.CompletedTask;
    }
}
