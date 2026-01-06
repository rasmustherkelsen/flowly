using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.RecurringJobs;

namespace Flowly.Jobs.Maintenance;

internal class RemoveOldJobsRecurringJob(IJobStateRepository jobStateRepository) : IRecurringJobHandler
{
    public async Task Handle(CancellationToken cancellationToken)
        => await jobStateRepository.RemoveJobsOlderThan(TimeSpan.FromDays(3));
}