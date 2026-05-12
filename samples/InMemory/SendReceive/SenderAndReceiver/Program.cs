using Flowly;
using Flowly.InMemory;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(null, flowlyBuilder =>
{
    flowlyBuilder
        .UseInMemory()
        .AddMessageSubmitter<HelloWorldMessage>()
        .AddMessageHandler<HelloWorldMessage, HelloWorldHandler>();
});

builder.Services.AddHostedService<SenderService>();

var app = builder.Build();

app.Run();

internal record HelloWorldMessage(string Payload);

internal class HelloWorldHandler : MessageHandler<HelloWorldMessage>
{
    public override Task Handle(IMessageContext<HelloWorldMessage> messageContext)
    {
        Console.WriteLine($"Received message with text: {messageContext.Message.Payload}");
        return Task.CompletedTask;
    }
}

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