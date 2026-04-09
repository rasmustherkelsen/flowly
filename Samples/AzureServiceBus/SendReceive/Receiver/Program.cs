using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.AzureServiceBus;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    flowlyBuilder =>
    {
        flowlyBuilder.UseAzureServiceBus();
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