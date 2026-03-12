using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.RecurringJobs;

namespace Flowly.Jobs.Maintenance;

[RecurringJob("Remove Old Jobs", "0 */1 * * *")]
internal class RemoveOldJobsRecurringJob(IJobStateRepository jobStateRepository) : RecurringJobHandlerBase
{
    public override async Task Handle(CancellationToken cancellationToken)
        => await jobStateRepository.RemoveJobsOlderThan(TimeSpan.FromDays(3));
}