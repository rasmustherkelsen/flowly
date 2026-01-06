namespace Flowly.Jobs;

internal static class QueuesNames
{
    public const string CreateJobState = "create-job-state";
    public const string CreateRecurringJobState = "create-recurring-job-state";
    public const string UpdateJobState = "update-job-state";
    public const string JobFailed = "job-failed";
    public const string UpdateJobCustomState = "update-job-custom-state";
    public const string RecurringJobs = "recurring-jobs";
    public const string StartRecurringJob = "start-recurring-job";
}
