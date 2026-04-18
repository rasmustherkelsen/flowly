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
        .AddEventHandler<OrderSubmittedMessage, NotifyEmployeeWhenOrderSubmittedHandler>());

var app = builder.Build();

app.Run();

internal class NotifyEmployeeWhenOrderSubmittedHandler(ILogger<NotifyEmployeeWhenOrderSubmittedHandler> logger) : EventHandlerBase<OrderSubmittedMessage>
{
    public override Task Handle(IEventContext<OrderSubmittedMessage> eventContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Notifying employee regarding order {OrderId} being submitted", eventContext.Event.OrderId);
        return Task.CompletedTask;
    }
}