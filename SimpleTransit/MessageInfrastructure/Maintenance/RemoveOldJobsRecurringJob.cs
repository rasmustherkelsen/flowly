using SimpleTransit.MessageInfrastructure.RecurringJobs;
using SimpleTransit.Repositories;

namespace SimpleTransit.MessageInfrastructure.Maintenance;

internal class RemoveOldJobsRecurringJob(IJobStateRepository jobStateRepository) : IRecurringJobHandler
{
    public async Task Handle(CancellationToken cancellationToken)
        => await jobStateRepository.RemoveJobsOlderThan(TimeSpan.FromDays(3));
}