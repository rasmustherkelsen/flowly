using Flowly;
using Messages;

namespace Sender.Services;

internal class MessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var caller = scope.ServiceProvider.GetRequiredService<IMessageCaller>();

            MyReturnMessage response = await caller.Call<MyMessage, MyReturnMessage>(
                new MyMessage($"Hello from Sender at {DateTime.Now}"), stoppingToken);

            Console.WriteLine($"Received response: {response.Reply}");

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
