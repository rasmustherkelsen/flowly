using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Services;

namespace Flowly.Jobs.Tests.Fakes;

internal class FakeJobStateRepository : IJobStateRepository
{
    public List<FlowlysysCreateJobStateMessage> CreatedJobStates { get; } = [];
    public List<(FlowlysysCreateRecurringJobStateMessage Message, JobId JobId)> CreatedRecurringJobStates { get; } = [];
    public List<FlowlysysUpdateJobStateMessage> UpdatedJobStates { get; } = [];
    public List<FlowlysysJobFailedMessage> FailedJobs { get; } = [];
    public List<TimeSpan> CompletedJobsRemovedOlderThan { get; } = [];
    public List<TimeSpan> FailedJobsRemovedOlderThan { get; } = [];
    public List<TimeSpan> UncompletedFailedOlderThan { get; } = [];
    public List<JobQuery> QueriesExecuted { get; } = [];

    public IReadOnlyCollection<RecurringJob> RecurringJobsToReturn { get; set; } = [];
    public IReadOnlyCollection<JobInfo> JobInfosToReturn { get; set; } = [];
    public Action? OnGetRecurringJobsCalled { get; set; }

    public Task CreateJobState(FlowlysysCreateJobStateMessage flowlysysCreateJobStateMessage)
    {
        CreatedJobStates.Add(flowlysysCreateJobStateMessage);
        return Task.CompletedTask;
    }

    public Task CreateRecurringJobState(FlowlysysCreateRecurringJobStateMessage flowlysysCreateRecurringJobStateMessage, JobId jobId)
    {
        CreatedRecurringJobStates.Add((flowlysysCreateRecurringJobStateMessage, jobId));
        return Task.CompletedTask;
    }

    public Task UpdateJobState(FlowlysysUpdateJobStateMessage flowlysysUpdateJobStateMessage)
    {
        UpdatedJobStates.Add(flowlysysUpdateJobStateMessage);
        return Task.CompletedTask;
    }

    public Task UpdateJobFailed(FlowlysysJobFailedMessage flowlysysJobFailedMessage)
    {
        FailedJobs.Add(flowlysysJobFailedMessage);
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
