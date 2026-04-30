namespace Flowly.Jobs;

internal static class JobQueuesNames
{
    public const string CreateJobState = "flowlysys-create-job-state";
    public const string CreateRecurringJobState = "flowlysys-create-recurring-job-state";
    public const string UpdateJobState = "flowlysys-update-job-state";
    public const string JobFailed = "flowlysys-job-failed";
    public const string UpdateJobCustomState = "flowlysys-update-job-custom-state";
    public const string RecurringJobs = "flowlysys-recurring-jobs";
    public const string StartRecurringJob = "flowlysys-start-recurring-job";
    public const string JobIsAlive = "flowlysys-job-is-alive";
}
