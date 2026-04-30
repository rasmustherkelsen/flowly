using Flowly;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.RabbitMQ;
using Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = true,
    flowlyBuilder => flowlyBuilder
        .UseRabbitMq()
        .AddPostgresDeadLetterTracking(
            builder.Configuration.GetConnectionString("FlowlyDeadLetters")!,
            true,
            deadLetterOptions =>
            {
                deadLetterOptions.DeleteDeadLetteredMessagesAfter = TimeSpan.FromMinutes(5);
                deadLetterOptions.DeleteRequeuedMessagesAfter = TimeSpan.FromMinutes(1);
            })
        .AddEventHandler<OrderSubmittedMessage, SendMailWhenOrderSubmittedHandler>()
        .WithDeadLetterTracking());

var host = builder.Build();

host.Run();

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