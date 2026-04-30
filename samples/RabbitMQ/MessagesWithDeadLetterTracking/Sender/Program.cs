using Flowly;
using Flowly.RabbitMQ;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(x => x.CreateTopology = true, flowlyBuilder =>
{
    flowlyBuilder
        .UseRabbitMq()
        .AddMessageSubmitter<FlakyMessage>();
});

builder.Services.AddHostedService<MessageSenderBackgroundService>();

var app = builder.Build();

app.Run();

class MessageSenderBackgroundService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = serviceScopeFactory.CreateScope();
        var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        while (!stoppingToken.IsCancellationRequested)
        {
            await messageSender.Send(new FlakyMessage(Random.Shared.Next(1, 3)), stoppingToken);
            await Task.Delay(TimeSpan.FromMilliseconds(10), stoppingToken);
        }
    }
}