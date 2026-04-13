using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
using Flowly.AzureServiceBus;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    flowlyBuilder => flowlyBuilder
        .UseAzureServiceBus()
        .AddMessageSubmitter<HelloWorldMessage>());

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
            var message = new HelloWorldMessage($"Hello, World! {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await messageSender.Send(message, stoppingToken);
            Console.WriteLine("Sent message with text: " + message.Payload);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}