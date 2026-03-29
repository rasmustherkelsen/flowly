namespace Flowly.Jobs.Services;

public interface IJobTrackingService
{
    Task<IReadOnlyCollection<RecurringJobInfo>> GetRecurringJobs(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<JobInfo>> GetJobs(CancellationToken cancellationToken = default);
}
