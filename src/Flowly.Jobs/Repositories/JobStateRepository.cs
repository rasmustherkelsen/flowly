using System.Collections.Concurrent;
using Flowly.Jobs.DatabaseModel;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.Repositories;

internal class JobStateRepository(IDbContextFactory<JobStateDataContext> jobStateDataContextFactory) : IJobStateRepository
{
    private static readonly ConcurrentDictionary<string, long> JobTypeCache = new();

    private static readonly SemaphoreSlim ReadSemaphore = new(1, 1);

    public async Task CreateJobState(CreateJobState createJobState)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        var job = new Job
        {
            JobIdentifier = createJobState.JobId.InnerId,
            Created = createJobState.TimeStamp,
            JobTypeId = await GetOrCreateJobTypeId(context, createJobState.JobTypeName),
            JobTypeName =  createJobState.JobTypeName,
            Description = createJobState.Description
        };

        await context.Jobs.AddAsync(job);
        
        await context.SaveChangesAsync();
    }

    public async Task CreateRecurringJobState(CreateRecurringJobState createRecurringJobState, JobId jobId)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        if (await context.Jobs.AnyAsync(x => x.JobType!.Name == createRecurringJobState.JobTypeName))
        {
            var cronExpression = createRecurringJobState.CronExpression;

            await context.Jobs
                .Where(x => x.JobType!.Name == createRecurringJobState.JobTypeName)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(p => p.CronExpression, _ => cronExpression)
                    .SetProperty(p => p.Completed, _ => null)
                    .SetProperty(p => p.Started, _ => null)
                    .SetProperty(p => p.CurrentState, _ => JobState.Created));

            return;
        }

        var job = new Job
        {
            JobIdentifier = jobId.InnerId,
            Created = createRecurringJobState.TimeStamp,
            JobTypeId = await GetOrCreateJobTypeId(context, createRecurringJobState.JobTypeName),
            JobTypeName =  createRecurringJobState.JobTypeName,
            Description = createRecurringJobState.Description,
            IsRecurringJob = true,
            CronExpression = createRecurringJobState.CronExpression
        };

        await context.Jobs.AddAsync(job);

        await context.SaveChangesAsync();
    }

    public async Task UpdateJobState(UpdateJobState updateJobState)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        var job = await ResilientGetJob(context, updateJobState.JobId);

        job.CurrentState = updateJobState.JobState;

        switch (updateJobState.JobState)
        {
            case JobState.Started:
                job.Started = updateJobState.TimeStamp;
                job.Completed = null;
                job.RetryAttempt = updateJobState.RetryAttempt;
                break;

            case JobState.Completed:
                job.Completed = updateJobState.TimeStamp;
                job.FaultReason = null;
                break;

            default:
                throw new InvalidOperationException($"Unsupported JobState: {updateJobState.JobState}");
        }

        await context.SaveChangesAsync();
    }

    public async Task UpdateJobFailed(JobFailed jobFailed)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        var faultReason = jobFailed.FaultReason;
        DateTimeOffset? completedAt = jobFailed.TimeStamp;

        await context.Jobs
            .Where(x => x.JobIdentifier == jobFailed.JobId.InnerId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.CurrentState, _ => JobState.Failed)
                .SetProperty(p => p.FaultReason, _ => faultReason)
                .SetProperty(p => p.Completed, _ => completedAt));
    }

    public async Task RemoveCompletedJobsOlderThan(TimeSpan age)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        var cutOffTime = DateTimeOffset.UtcNow - age;

        await context.Jobs
            .Where(x => x.IsRecurringJob == false && x.CurrentState == JobState.Completed && x.Completed < cutOffTime)
            .ExecuteDeleteAsync();
    }

    public async Task RemoveFailedJobsOlderThan(TimeSpan age)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        var cutOffTime = DateTimeOffset.UtcNow - age;

        await context.Jobs
            .Where(x => x.IsRecurringJob == false && x.CurrentState == JobState.Failed && x.Completed < cutOffTime)
            .ExecuteDeleteAsync();
    }

    public async Task<IReadOnlyCollection<JobInfo>> Query(JobQuery query)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        var q = context.Jobs.AsNoTracking().Where(x => x.IsRecurringJob == false);

        if (query.JobId.HasValue)
            q = q.Where(x => x.JobIdentifier == query.JobId.Value);

        if (query.JobType is not null)
            q = q.Where(x => x.JobTypeName == query.JobType);

        return await q
            .Select(j => new JobInfo(
                j.JobIdentifier,
                j.JobTypeName,
                j.Description,
                j.CurrentState,
                j.Created,
                j.Started,
                j.Completed,
                j.FaultReason,
                j.RetryAttempt))
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<RecurringJob>> GetRecurringJobs()
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        return await context.Jobs
            .Where(x => x.IsRecurringJob == true)
            .Select(j => new RecurringJob(j.JobIdentifier, j.JobType!.Name, j.Description, j.CronExpression!, j.Created, j.Started, j.Completed))
            .ToListAsync();
    }

    public async Task FailUncompletedJobsOlderThan(TimeSpan age)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        DateTimeOffset? now = DateTimeOffset.UtcNow;
        var cutoffTime = now.Value - age;

        await context.Jobs
            .Where(x => x.IsRecurringJob == false &&
                        x.CurrentState != JobState.Completed &&
                        x.CurrentState != JobState.Failed &&
                        x.Started < cutoffTime)
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.Completed, _ => now)
                .SetProperty(p => p.FaultReason, _ => "Hung job")
                .SetProperty(p => p.CurrentState, _ => JobState.Failed));
    }

    private async Task<Job> ResilientGetJob(JobStateDataContext context, JobId jobId)
    {
        int retryCount = 0;

        retry:
        var job = await context.Jobs.SingleOrDefaultAsync(x => x.JobIdentifier == jobId.InnerId);
        if (job == null)
        {
            if (retryCount == 10)
                throw new InvalidOperationException("Unable to get job for state update");

            await Task.Delay(TimeSpan.FromSeconds(1));
            retryCount++;
            goto retry;
        }

        return job;
    }

    private async Task<long> GetOrCreateJobTypeId(JobStateDataContext jobStateDataContext, string jobTypeName)
    {
        retry:

        if (JobTypeCache.TryGetValue(jobTypeName, out long id))
            return id;

        if (await ReadSemaphore.WaitAsync(TimeSpan.FromSeconds(1)) == false)
            goto retry;

        try
        {
            var jobType = await jobStateDataContext.JobTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name == jobTypeName);

            if (jobType == null)
            {
                jobType = new JobType
                {
                    Name = jobTypeName
                };

                jobStateDataContext.JobTypes.Add(jobType);

                await jobStateDataContext.SaveChangesAsync();
            }

            JobTypeCache.TryAdd(jobTypeName, jobType.Id);

            return jobType.Id;
        }
        finally
        {
            ReadSemaphore.Release();
        }
    }
}