using Flowly.Jobs.Repositories;
using Flowly.Jobs.Senders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.Jobs.BackgroundServices;

internal class RecurringJobSchedulerBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    RecurringJobSchedulerBackgroundServiceOptions recurringJobSchedulerBackgroundServiceOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var jobStateRepository = scope.ServiceProvider.GetRequiredService<IJobStateRepository>();

        var recurringJobInvoker = scope.ServiceProvider.GetRequiredService<IRecurringJobInvoker>();

        while (stoppingToken.IsCancellationRequested == false)
        {
            var recurringJobs = await jobStateRepository.GetRecurringJobs();

            foreach (var recurringJob in recurringJobs.Where(r => r.IsDue()))
            {
                await recurringJobInvoker.Submit(recurringJob);
            }

            await Task.Delay(recurringJobSchedulerBackgroundServiceOptions.DelayBetweenChecks, stoppingToken);
        }
    }
}