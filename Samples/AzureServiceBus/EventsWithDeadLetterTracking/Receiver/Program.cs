using Flowly.AzureServiceBus;
using Flowly.DeadLetters.Registration;
using Flowly.DeadLetters.SqlServer.Registration;
using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
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

[RetryPolicy(maxRetries: 3, delaySeconds: 5)]
internal class SendMailWhenOrderSubmittedHandler(ILogger<SendMailWhenOrderSubmittedHandler> logger) : EventHandlerBase<OrderSubmittedMessage>
{
    public override Task Handle(IEventContext<OrderSubmittedMessage> eventContext, CancellationToken cancellationToken)
    {
        bool shouldCrash = Random.Shared.Next(0, 5) < 4;

        if (shouldCrash)
        {
            throw new InvalidOperationException("Simulated crash for testing purposes");
        }

        logger.LogInformation("Sending e-mail to customer regarding order {OrderId} being submitted", eventContext.Event.OrderId);
        return Task.CompletedTask;
    }
}