using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Flowly.RabbitMQ;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = true,
    flowlyBuilder => flowlyBuilder
        .UseRabbitMq()
        .AddEventSubmitter<OrderSubmittedMessage>());

builder.Services.AddHostedService<RaiseEventBackgroundService>();

var app = builder.Build();

app.Run();

internal class RaiseEventBackgroundService(ILogger<RaiseEventBackgroundService> logger, IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var eventSender = scope.ServiceProvider.GetRequiredService<IEventSender>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var timeStamp = DateTime.UtcNow;
            await eventSender.RaiseEvent(new OrderSubmittedMessage(Guid.NewGuid(), $"Order event raised at {timeStamp}"), stoppingToken);
            logger.LogInformation("Raised order event at {TimeStamp}", timeStamp);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
