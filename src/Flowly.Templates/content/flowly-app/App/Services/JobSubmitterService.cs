using Flowly.Jobs;
using App.Messages;

namespace App.Services;

internal class JobSubmitterService(IServiceScopeFactory serviceScopeFactory, ILogger<JobSubmitterService> logger) : BackgroundService
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

            logger.LogInformation("Queued job {JobId}", jobId);

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
