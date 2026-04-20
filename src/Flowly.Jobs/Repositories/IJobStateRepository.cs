using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Services;

namespace Flowly.Jobs.Repositories;

internal interface IJobStateRepository
{
    Task CreateJobState(CreateJobState createJobState);

    Task CreateRecurringJobState(CreateRecurringJobState createRecurringJobState, JobId jobId);

    Task UpdateJobState(UpdateJobState updateJobState);

    Task UpdateJobFailed(JobFailed jobFailed);

    Task RemoveCompletedJobsOlderThan(TimeSpan age);

    Task RemoveFailedJobsOlderThan(TimeSpan age);

    Task<IReadOnlyCollection<RecurringJob>> GetRecurringJobs();

    Task<IReadOnlyCollection<JobInfo>> Query(JobQuery query);

    Task FailUncompletedJobsOlderThan(TimeSpan age);
}
