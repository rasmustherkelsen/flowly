using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Services;

namespace Flowly.Jobs.Tests.Fakes;

internal class FakeJobStateRepository : IJobStateRepository
{
    public List<CreateJobState> CreatedJobStates { get; } = [];
    public List<(CreateRecurringJobState Message, JobId JobId)> CreatedRecurringJobStates { get; } = [];
    public List<UpdateJobState> UpdatedJobStates { get; } = [];
    public List<JobFailed> FailedJobs { get; } = [];
    public List<TimeSpan> CompletedJobsRemovedOlderThan { get; } = [];
    public List<TimeSpan> FailedJobsRemovedOlderThan { get; } = [];
    public List<TimeSpan> UncompletedFailedOlderThan { get; } = [];
    public List<JobQuery> QueriesExecuted { get; } = [];

    public IReadOnlyCollection<RecurringJob> RecurringJobsToReturn { get; set; } = [];
    public IReadOnlyCollection<JobInfo> JobInfosToReturn { get; set; } = [];
    public Action? OnGetRecurringJobsCalled { get; set; }

    public Task CreateJobState(CreateJobState createJobState)
    {
        CreatedJobStates.Add(createJobState);
        return Task.CompletedTask;
    }

    public Task CreateRecurringJobState(CreateRecurringJobState createRecurringJobState, JobId jobId)
    {
        CreatedRecurringJobStates.Add((createRecurringJobState, jobId));
        return Task.CompletedTask;
    }

    public Task UpdateJobState(UpdateJobState updateJobState)
    {
        UpdatedJobStates.Add(updateJobState);
        return Task.CompletedTask;
    }

    public Task UpdateJobFailed(JobFailed jobFailed)
    {
        FailedJobs.Add(jobFailed);
        return Task.CompletedTask;
    }

    public Task RemoveCompletedJobsOlderThan(TimeSpan age)
    {
        CompletedJobsRemovedOlderThan.Add(age);
        return Task.CompletedTask;
    }

    public Task RemoveFailedJobsOlderThan(TimeSpan age)
    {
        FailedJobsRemovedOlderThan.Add(age);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<RecurringJob>> GetRecurringJobs()
    {
        OnGetRecurringJobsCalled?.Invoke();
        return Task.FromResult(RecurringJobsToReturn);
    }

    public Task<IReadOnlyCollection<JobInfo>> Query(JobQuery query)
    {
        QueriesExecuted.Add(query);
        return Task.FromResult(JobInfosToReturn);
    }

    public Task FailUncompletedJobsOlderThan(TimeSpan age)
    {
        UncompletedFailedOlderThan.Add(age);
        return Task.CompletedTask;
    }
}
