using Flowly;
using Flowly.AzureServiceBus;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    flowlyBuilder => flowlyBuilder
        .UseAzureServiceBus()
        .AddMessageSubmitter<FlakyMessage>());

builder.Services.AddHostedService<MessageSenderBackgroundService>();

var app = builder.Build();

app.Run();

class MessageSenderBackgroundService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        while (!stoppingToken.IsCancellationRequested)
        {
            await messageSender.Send(new FlakyMessage(Random.Shared.Next(1, 3)), stoppingToken);
            await Task.Delay(TimeSpan.FromMilliseconds(10), stoppingToken);
        }
    }
}
