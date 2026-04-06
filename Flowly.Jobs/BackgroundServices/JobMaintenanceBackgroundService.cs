using Flowly.Jobs.Repositories;
using Flowly.Jobs.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Flowly.Jobs.BackgroundServices;

internal class JobMaintenanceBackgroundService(IServiceScopeFactory serviceScopeFactory, IOptions<JobStateTrackingOptions> options) : BackgroundService
{
    private static readonly TimeSpan HungJobThreshold = TimeSpan.FromHours(3);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IJobStateRepository>();
            var opts = options.Value;

            await repository.FailUncompletedJobsOlderThan(HungJobThreshold);

            if (opts.DeleteCompletedJobsAfter.HasValue)
                await repository.RemoveCompletedJobsOlderThan(opts.DeleteCompletedJobsAfter.Value);

            if (opts.DeleteFailedJobsAfter.HasValue)
                await repository.RemoveFailedJobsOlderThan(opts.DeleteFailedJobsAfter.Value);
        }
    }
}
