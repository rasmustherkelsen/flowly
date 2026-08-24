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

            var recorder = scope.ServiceProvider.GetRequiredService<IMessageRecorder>();

            var partitionKey = $"sensor-{DateTime.Now.Second % 4}";

            await recorder.Record(new MyMessage($"Hello from Sender at {DateTime.Now}"), stoppingToken, partitionKey);

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
