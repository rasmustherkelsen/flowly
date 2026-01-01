using Flowly.MessageInfrastructure.RecurringJobs;
using Flowly.Repositories;

namespace Flowly.MessageInfrastructure.Maintenance;

internal class RemoveOldJobsRecurringJob(IJobStateRepository jobStateRepository) : IRecurringJobHandler
{
    public async Task Handle(CancellationToken cancellationToken)
        => await jobStateRepository.RemoveJobsOlderThan(TimeSpan.FromDays(3));
}