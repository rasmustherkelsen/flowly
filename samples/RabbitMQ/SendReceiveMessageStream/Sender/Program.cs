using Flowly;
using Flowly.MessageInfrastructure;
using Messages;
using Sender;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.WithTopologyNameResolver<DotCaseTopologyNameResolver>());

builder.Services.AddHostedService<SenderService>();

var app = builder.Build();
app.Run();

class SenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var messageRecord = scope.ServiceProvider.GetRequiredService<IMessageRecorder>();
        
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MyStreamMessage>>();

        long counter = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = new MyStreamMessage(counter++);
            await messageRecord.Record(message, stoppingToken);
            logger.LogInformation("Recoreded entry with number: {MessageEntryNumber}", message.EntryNumber);
            await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
        }
    }
}