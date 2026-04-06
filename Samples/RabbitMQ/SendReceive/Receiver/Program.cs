using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.RabbitMQ;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(options => options.CreateTopology = true);

var app = builder.Build();

app.Run();

internal class FlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq();
        builder.AddMessageHandler<HelloWorldMessage, HelloWorldHandler>();
    }
}

internal class HelloWorldHandler : MessageHandler<HelloWorldMessage>
{
    public override Task Handle(IMessageContext<HelloWorldMessage> messageContext)
    {
        Console.WriteLine($"Received message with text: {messageContext.Message.Payload}");
        return Task.CompletedTask;
    }
}