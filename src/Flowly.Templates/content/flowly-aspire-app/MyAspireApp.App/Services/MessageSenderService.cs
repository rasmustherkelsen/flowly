using Flowly;
using MyAspireApp.App.Messages;

namespace MyAspireApp.App.Services;

internal class MessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

#if (UseCallHandler)
            var caller = scope.ServiceProvider.GetRequiredService<IMessageCaller>();

            MyReturnMessage response = await caller.Call<MyMessage, MyReturnMessage>(
                new MyMessage($"Hello at {DateTime.Now}"), stoppingToken);

            Console.WriteLine($"Received response: {response.Reply}");
#else
            var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            await sender.Send(new MyMessage($"Hello at {DateTime.Now}"), stoppingToken);
#endif

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
