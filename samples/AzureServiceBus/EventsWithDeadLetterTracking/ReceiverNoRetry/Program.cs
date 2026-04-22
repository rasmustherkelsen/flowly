using Flowly;
using Flowly.AzureServiceBus;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    flowlyBuilder => flowlyBuilder.CreateTopology = false,
    options => options
        .UseAzureServiceBus()
        .AddEventHandler<OrderSubmittedMessage, DoSomethingWhenOrderSubmittedHandler>());

var app = builder.Build();

app.Run();

internal class DoSomethingWhenOrderSubmittedHandler(ILogger<DoSomethingWhenOrderSubmittedHandler> logger) : EventHandlerBase<OrderSubmittedMessage>
{
    public override Task Handle(IEventContext<OrderSubmittedMessage> eventContext, CancellationToken cancellationToken)
    {
        var shouldCrash = Random.Shared.Next(0, 5) < 4;

        if (shouldCrash) throw new InvalidOperationException("Simulated crash for testing purposes");

        logger.LogInformation("Did something regarding order {OrderId} being submitted", eventContext.Event.OrderId);
        return Task.CompletedTask;
    }
}