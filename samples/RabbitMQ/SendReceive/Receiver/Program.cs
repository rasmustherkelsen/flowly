using Flowly;
using Flowly.RabbitMQ;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = true,
    flowlyBuilder =>
    {
        flowlyBuilder.UseRabbitMq();
        flowlyBuilder.AddMessageHandler<HelloWorldMessage, HelloWorldHandler>();
    });

var app = builder.Build();

app.Run();

internal class HelloWorldHandler : MessageHandler<HelloWorldMessage>
{
    public override Task Handle(IMessageContext<HelloWorldMessage> messageContext)
    {
        Console.WriteLine($"Received message with text: {messageContext.Message.Payload}");
        return Task.CompletedTask;
    }
}