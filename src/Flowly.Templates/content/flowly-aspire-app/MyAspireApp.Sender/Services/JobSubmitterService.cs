using Flowly.Jobs;
using MyAspireApp.Messages;

namespace MyAspireApp.Sender.Services;

internal class JobSubmitterService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var sender = scope.ServiceProvider.GetRequiredService<IJobMessageSender>();
            var jobId = await sender.QueueJob(
                new ProcessJobMessage($"Task at {DateTime.Now}"),
                stoppingToken);

            Console.WriteLine($"Queued job {jobId}");

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
