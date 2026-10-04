using Flowly.Jobs;
using MyAspireApp.App.Messages;

namespace MyAspireApp.App.Services;

internal class JobSubmitterService(IServiceScopeFactory serviceScopeFactory, ILogger<JobSubmitterService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();

        var sender = scope.ServiceProvider.GetRequiredService<IJobMessageSender>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var jobId = await sender.QueueJob(
                new ProcessJobMessage($"Task at {DateTime.Now}"),
                stoppingToken);

            logger.LogInformation("Queued job {JobId}", jobId);

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
