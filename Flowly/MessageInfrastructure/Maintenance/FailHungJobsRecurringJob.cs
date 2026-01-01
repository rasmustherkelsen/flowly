using Flowly.MessageInfrastructure.RecurringJobs;
using Flowly.Repositories;

namespace Flowly.MessageInfrastructure.Maintenance;

internal class FailHungJobsRecurringJob(IJobStateRepository jobStateRepository) : IRecurringJobHandler
{
    public async Task Handle(CancellationToken cancellationToken)
        => await jobStateRepository.FailUncompletedJobsOlderThan(TimeSpan.FromHours(3));
}
