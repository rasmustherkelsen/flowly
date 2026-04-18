using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Flowly.RabbitMQ;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = true,
    flowlyBuilder => flowlyBuilder
        .UseRabbitMq()
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
