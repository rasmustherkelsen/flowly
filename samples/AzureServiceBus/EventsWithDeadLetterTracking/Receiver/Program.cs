using Flowly;
using Flowly.AzureServiceBus;
using Flowly.MessageInfrastructure.Receivers;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    flowlyBuilder => flowlyBuilder.CreateTopology = false,
    options => options
        .UseAzureServiceBus()
        .AddSqlServerDeadLetterTracking(
            builder.Configuration.GetConnectionString("FlowlyDeadLetters")!,
            true,
            deadLetterOptions =>
            {
                deadLetterOptions.DeleteDeadLetteredMessagesAfter = TimeSpan.FromMinutes(5);
                deadLetterOptions.DeleteRequeuedMessagesAfter = TimeSpan.FromMinutes(1);
            })
        .AddEventHandler<OrderSubmittedMessage, SendMailWhenOrderSubmittedHandler>()
        .WithDeadLetterTracking());

var app = builder.Build();

app.Run();

[RetryPolicy(3, 5)]
internal class SendMailWhenOrderSubmittedHandler(ILogger<SendMailWhenOrderSubmittedHandler> logger) : EventHandlerBase<OrderSubmittedMessage>
{
    public override Task Handle(IEventContext<OrderSubmittedMessage> eventContext, CancellationToken cancellationToken)
    {
        var shouldCrash = Random.Shared.Next(0, 5) < 4;

        if (shouldCrash) throw new InvalidOperationException("Simulated crash for testing purposes");

        logger.LogInformation("Sending e-mail to customer regarding order {OrderId} being submitted", eventContext.Event.OrderId);
        return Task.CompletedTask;
    }
}