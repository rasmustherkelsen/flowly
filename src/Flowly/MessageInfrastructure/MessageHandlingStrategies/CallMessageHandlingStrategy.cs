using System.Diagnostics;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.MessageHandlingStrategies;

internal class CallMessageHandlingStrategy<TMessage, TReturn>(
    IMessageBusClientRegistry clientRegistry,
    IHandlerSettings<TMessage> handlerSettings,
    IHandlerInstrumentation handlerInstrumentation) : IMessageHandlingStrategy<TMessage>
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
        var messageId = Guid.NewGuid().ToString();
        var sw = Stopwatch.StartNew();

        using var activity = handlerInstrumentation.StartSendingResponse(
            handlerSettings.QueueName, replyTo, client.MessagingSystem,
            messageId, receivedMessage.Properties.CorrelationId);
        activity.ApplyTagsFrom(returnMessage);

        try
        {
            await sender.SendMessage(
                returnMessage,
                new MessageProperties(messageId, receivedMessage.Properties.CorrelationId),
                cancellationToken);

            handlerInstrumentation.RecordResponseSent(handlerSettings.QueueName, sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            handlerInstrumentation.RecordResponseFailed(handlerSettings.QueueName);
            throw;
        }
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
