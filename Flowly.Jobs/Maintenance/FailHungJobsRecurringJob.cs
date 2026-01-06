using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.RecurringJobs;

namespace Flowly.Jobs.Maintenance;

internal class FailHungJobsRecurringJob(IJobStateRepository jobStateRepository) : IRecurringJobHandler
{
    public async Task Handle(CancellationToken cancellationToken)
        => await jobStateRepository.FailUncompletedJobsOlderThan(TimeSpan.FromHours(3));
}
