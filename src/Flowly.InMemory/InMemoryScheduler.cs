using Microsoft.Extensions.Hosting;

namespace Flowly.InMemory;

internal class InMemoryScheduler(InMemoryBroker broker) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            broker.DeliverReadyScheduledMessages();

            try
            {
                await Task.Delay(100, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
