using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.RecurringJobs;

namespace Flowly.Jobs.Maintenance;

[RecurringJob("Fail hung jobs", "*/30 * * * *")]
internal class FailHungJobsRecurringJob(IJobStateRepository jobStateRepository) : RecurringJobHandlerBase
{
    public override async Task Handle(CancellationToken cancellationToken)
        => await jobStateRepository.FailUncompletedJobsOlderThan(TimeSpan.FromHours(3));
}
