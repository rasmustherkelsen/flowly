using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
using Flowly.AzureServiceBus;
using Flowly.RabbitMQ;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    flowlyBuilder => flowlyBuilder
        .UseAzureServiceBus()
        .UseRabbitMq("amqp://guest:guest@localhost:5673/", "RabbitMQ", createTopology: true)
        .AddMessageSubmitter<HelloWorldBusOne>()
        .AddMessageSubmitter<HelloWorldBusTwo>());

builder.Services.AddHostedService<SenderService>();

var app = builder.Build();
app.Run();

class SenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var messageOne = new HelloWorldBusOne($"Hello, World! {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await messageSender.Send(messageOne, stoppingToken);
            Console.WriteLine("Sent message with text: " + messageOne.Payload);
            
            var messageTwo = new HelloWorldBusTwo($"Hello, World! {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await messageSender.Send(messageTwo, stoppingToken);
            Console.WriteLine("Sent message with text: " + messageTwo.Payload);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}