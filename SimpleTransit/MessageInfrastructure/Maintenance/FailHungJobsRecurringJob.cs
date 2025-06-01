using SimpleTransit.MessageInfrastructure.RecurringJobs;
using SimpleTransit.Repositories;

namespace SimpleTransit.MessageInfrastructure.Maintenance;

internal class FailHungJobsRecurringJob(IJobStateRepository jobStateRepository) : IRecurringJobHandler
{
    public async Task Handle(CancellationToken cancellationToken)
        => await jobStateRepository.FailUncompletedJobsOlderThan(TimeSpan.FromHours(3));
}
