using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Flowly.RabbitMQ;
using Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = true,
    flowlyBuilder => flowlyBuilder
        .UseRabbitMq()
        .AddEventHandler<OrderSubmittedMessage, DoSomethingWhenOrderSubmittedHandler>());

var host = builder.Build();

host.Run();

internal class DoSomethingWhenOrderSubmittedHandler(ILogger<DoSomethingWhenOrderSubmittedHandler> logger) : EventHandlerBase<OrderSubmittedMessage>
{
    public override Task Handle(IEventContext<OrderSubmittedMessage> eventContext, CancellationToken cancellationToken)
    {
        bool shouldCrash = Random.Shared.Next(0, 5) < 3;

        if (shouldCrash)
        {
            throw new InvalidOperationException("Simulated crash for testing purposes");
        }

        logger.LogInformation("Did something regarding order {OrderId} being submitted", eventContext.Event.OrderId);
        return Task.CompletedTask;
    }
}
