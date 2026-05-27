using Flowly;
using App.Messages;

namespace App.Services;

internal class MessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            await sender.Send(new MyMessage($"Hello at {DateTime.Now}"), stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
