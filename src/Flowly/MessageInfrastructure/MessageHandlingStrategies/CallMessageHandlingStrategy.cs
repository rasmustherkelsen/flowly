using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.MessageHandlingStrategies;

internal class CallMessageHandlingStrategy<TMessage, TReturn>(
    IMessageBusClientRegistry clientRegistry,
    IHandlerSettings<TMessage> handlerSettings) : IMessageHandlingStrategy<TMessage>
    where TMessage : class, IReturns<TReturn>
    where TReturn : class
{
    public async Task HandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var replyTo = receivedMessage.Properties.ReplyTo
            ?? throw new InvalidOperationException(
                $"Call message of type '{typeof(TMessage).Name}' is missing the ReplyTo property. " +
                "Ensure the message was sent via IMessageCaller.Call.");

        var handler = serviceProvider.GetRequiredService<CallHandler<TMessage, TReturn>>();
        var returnMessage = await handler.InvokeHandle(new MessageContext<TMessage>(receivedMessage.Body, cancellationToken));

        var client = clientRegistry.GetClient(handlerSettings.ProviderName);
        var sender = await client.CreateMessageBusSender(replyTo);

        await sender.SendMessage(
            returnMessage,
            new MessageProperties(Guid.NewGuid().ToString(), receivedMessage.Properties.CorrelationId),
            cancellationToken);
    }

    public async Task OnRetriesExhausted(IReceivedMessage<TMessage> receivedMessage, Exception exception, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await receivedMessage.DeadLetter(exception.Message, cancellationToken);
    }

    public Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails)
    {
        logger.LogError("{Message}", errorDetails.Exception.Message);
        return Task.CompletedTask;
    }
}
