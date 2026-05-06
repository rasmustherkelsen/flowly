using Flowly.Jobs.Services;

namespace Flowly.Jobs;

/// <summary>
///     Service for tracking the status of jobs, including recurring jobs and one-time jobs. This service can be used to
///     query the current status of jobs, as well as to retrieve historical information about completed jobs.
/// </summary>
public interface IJobTrackingService
{
    /// <summary>
    ///     Gets a list of all recurring jobs, including their current status and next scheduled run time.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    ///     A <see cref="Task" /> that represents the asynchronous operation, returning a collection of
    ///     <see cref="RecurringJobInfo" /> objects.
    /// </returns>
    Task<IReadOnlyCollection<RecurringJobInfo>> GetRecurringJobs(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a list of all one-time jobs, including their current status and scheduled run time.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    ///     A <see cref="Task" /> that represents the asynchronous operation, returning a collection of
    ///     <see cref="JobInfo" /> objects.
    /// </returns>
    Task<IReadOnlyCollection<JobInfo>> GetJobs(CancellationToken cancellationToken = default);
}