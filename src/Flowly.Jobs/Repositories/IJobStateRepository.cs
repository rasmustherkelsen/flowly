using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Services;

namespace Flowly.Jobs.Repositories;

internal interface IJobStateRepository
{
    Task CreateJobState(FlowlysysCreateJobStateMessage flowlysysCreateJobStateMessage);

    Task CreateRecurringJobState(FlowlysysCreateRecurringJobStateMessage flowlysysCreateRecurringJobStateMessage, JobId jobId);

    Task UpdateJobState(FlowlysysUpdateJobStateMessage flowlysysUpdateJobStateMessage);

    Task UpdateJobFailed(FlowlysysJobFailedMessage flowlysysJobFailedMessage);

    Task RemoveCompletedJobsOlderThan(TimeSpan age);

    Task RemoveFailedJobsOlderThan(TimeSpan age);

    Task<IReadOnlyCollection<RecurringJob>> GetRecurringJobs();

    Task<IReadOnlyCollection<JobInfo>> Query(JobQuery query);

    Task FailUncompletedJobsOlderThan(TimeSpan age);
}
