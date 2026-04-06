using Flowly.Jobs.DatabaseModel;
using Flowly.Jobs.Model;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.Repositories;

internal class JobAliveStatusRepository(IDbContextFactory<JobStateDataContext> jobStateDataContextFactory) : IJobAliveStatusRepository
{
    public async Task CreateJobAliveStatus(JobId jobId, DateTimeOffset lastAliveTimeStamp)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        var jobAliveStatus = new JobAliveStatus
        {
            JobIdentifier = jobId.InnerId,
            LastAliveTimestamp = lastAliveTimeStamp
        };

        context.JobAliveStatuses.Add(jobAliveStatus);
        await context.SaveChangesAsync();
    }
    
    public async Task SetJobAliveStatus(JobId jobId, DateTimeOffset lastAliveTimeStamp)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        await context.JobAliveStatuses
            .Where(x => x.JobIdentifier == jobId.InnerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.LastAliveTimestamp, lastAliveTimeStamp));
    }
}