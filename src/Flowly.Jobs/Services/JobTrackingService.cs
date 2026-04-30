using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.Services;

internal class JobTrackingService(IJobStateRepository jobStateRepository) : IJobTrackingService
{
    public async Task<IReadOnlyCollection<RecurringJobInfo>> GetRecurringJobs(CancellationToken cancellationToken = default)
    {
        var jobs = await jobStateRepository.GetRecurringJobs();

        return jobs
            .Select(j => new RecurringJobInfo(j.JobId, j.JobTypeName, j.Description, j.CronExpression, j.Created, j.Started, j.Completed))
            .ToList();
    }

    public async Task<IReadOnlyCollection<JobInfo>> GetJobs(CancellationToken cancellationToken = default)
        => await jobStateRepository.Query(new JobQuery());
}
