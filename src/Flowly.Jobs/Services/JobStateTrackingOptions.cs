namespace Flowly.Jobs.Services;

public class JobStateTrackingOptions
{
    public TimeSpan? DeleteCompletedJobsAfter { get; set; }

    public TimeSpan? DeleteFailedJobsAfter { get; set; }
}
