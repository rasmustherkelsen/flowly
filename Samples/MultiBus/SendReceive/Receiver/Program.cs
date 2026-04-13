using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.AzureServiceBus;
using Flowly.RabbitMQ;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    flowlyBuilder =>
    {
        flowlyBuilder.UseAzureServiceBus();
        flowlyBuilder.UseRabbitMq("amqp://guest:guest@localhost:5673/", "RabbitMQ", createTopology: true);
        flowlyBuilder.AddMessageHandler<HelloWorldBusOne, HelloWorldBusOneHandler>();
        flowlyBuilder.AddMessageHandler<HelloWorldBusTwo, HelloWorldBusTwoHandler>();
    });

var app = builder.Build();

app.Run();

internal class HelloWorldBusOneHandler : MessageHandler<HelloWorldBusOne>
{
    public override Task Handle(IMessageContext<HelloWorldBusOne> messageContext)
    {
        Console.WriteLine($"Received message on Azure Service Bus with text: {messageContext.Message.Payload}");
        return Task.CompletedTask;
    }
}

internal class HelloWorldBusTwoHandler : MessageHandler<HelloWorldBusTwo>
{
    public override Task Handle(IMessageContext<HelloWorldBusTwo> messageContext)
    {
        Console.WriteLine($"Received message on RabbitMQ with text: {messageContext.Message.Payload}");
        return Task.CompletedTask;
    }
}