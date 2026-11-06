using Flowly.MessageInfrastructure.HandlerSteps;
using Flowly.MessageInfrastructure.Model;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.MessageHandlingStrategies;

internal class StandardMessageHandlingStrategy<TMessage>(IHandlerStepRunner handlerStepRunner) : IMessageHandlingStrategy<TMessage> where TMessage : class
{
    public async Task HandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var handler = serviceProvider.GetRequiredService<MessageHandler<TMessage>>();
        await handlerStepRunner.Run(
            receivedMessage.Body,
            receivedMessage.Properties,
            handler.GetType(),
            serviceProvider,
            cancellationToken,
            () => handler.Handle(new MessageContext<TMessage>(receivedMessage.Body, cancellationToken, receivedMessage.Properties)));
    }

    public async Task OnRetriesExhausted(IReceivedMessage<TMessage> receivedMessage, Exception exception, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await receivedMessage.DeadLetter(exception.Message, cancellationToken);
    }

    public Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails)
    {
        logger.LogError(errorDetails.Exception, "Message processor error");
        return Task.CompletedTask;
    }
}