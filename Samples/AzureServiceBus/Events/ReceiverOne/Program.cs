using Flowly.AzureServiceBus;
using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    flowlyBuilder => flowlyBuilder.CreateTopology = false,
    options => options
        .UseAzureServiceBus()
        .AddEventHandler<OrderSubmittedMessage, SendMailWhenOrderSubmittedHandler>());

var app = builder.Build();

app.Run();

internal class SendMailWhenOrderSubmittedHandler(ILogger<SendMailWhenOrderSubmittedHandler> logger) : EventHandlerBase<OrderSubmittedMessage>
{
    public override Task Handle(IEventContext<OrderSubmittedMessage> eventContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Sending e-mail to customer regarding order {OrderId} being submitted", eventContext.Event.OrderId);
        return Task.CompletedTask;
    }
}