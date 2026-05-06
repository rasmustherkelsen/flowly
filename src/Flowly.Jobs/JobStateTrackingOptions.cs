namespace Flowly.Jobs;

/// <summary>
///     Options for tracking the state of jobs. This includes settings for how long to keep completed and failed jobs
///     before they are deleted from the system.
/// </summary>
public class JobStateTrackingOptions
{
    /// <summary>
    ///     Gets or sets the duration after which completed jobs should be deleted from the system. If this value is null,
    ///     completed jobs will not be automatically deleted.
    /// </summary>
    public TimeSpan? DeleteCompletedJobsAfter { get; set; }

    /// <summary>
    ///     Gets or sets the duration after which failed jobs should be deleted from the system. If this value is null, failed
    ///     jobs will not be automatically deleted.
    /// </summary>
    public TimeSpan? DeleteFailedJobsAfter { get; set; }
}