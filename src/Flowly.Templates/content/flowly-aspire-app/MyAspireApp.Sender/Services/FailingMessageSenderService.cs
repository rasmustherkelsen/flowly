using Flowly;
using MyAspireApp.Messages;

namespace MyAspireApp.Sender.Services;

internal class FailingMessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            await sender.Send(new DeadLetterSampleMessage("[fail] Simulated bad payload"), stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
