using Flowly.Jobs.DatabaseModel;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.Repositories;

internal class JobStateQueryRepository(IDbContextFactory<JobStateDataContext> dbContextFactory) : IJobStateQueryRepository
{
    public async Task<bool> Exists(Guid jobId, bool? recurring = null)
    {
        await using var jobServiceSagaDbContext = await dbContextFactory.CreateDbContextAsync();
        var query = jobServiceSagaDbContext.Jobs.Where(x => x.JobId == jobId);

        if (recurring != null)
            query = query.Where(x => x.IsRecurringJob == recurring);

        return await query.AnyAsync(x => x.JobId == jobId);
    }

    public async Task<IReadOnlyCollection<JobInformation>> Query(JobQuery? jobQuery = null)
    {
        await using var jobServiceSagaDbContext = await dbContextFactory.CreateDbContextAsync();

        var query = jobServiceSagaDbContext.Jobs.AsQueryable();

        if (!string.IsNullOrEmpty(jobQuery?.JobType))
        {
            query = query.Where(x => x.JobType != null && x.JobType.Name == jobQuery.JobType);
        }

        if (jobQuery?.JobId != null) 
            query = query.Where(x => x.JobId == jobQuery.JobId);

        return await query.Select(s => new JobInformation(
            s.JobId,
            s.JobType!.Name,
            s.CurrentState,
            s.Description,
            s.Created,
            s.Started,
            s.Completed,
            s.FaultReason,
            s.IsRecurringJob,
            s.CronExpression,
            s.CustomJobState != null ? s.CustomJobState.CustomState : null)).ToListAsync();
    }
}