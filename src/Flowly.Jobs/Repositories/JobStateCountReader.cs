using Flowly.Jobs.DatabaseModel;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.Repositories;

internal sealed class JobStateCountReader(IDbContextFactory<JobStateDataContext> dbContextFactory) : IJobStateCountReader
{
    public async Task<long> CountJobsInState(JobState state, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Jobs.LongCountAsync(j => j.CurrentState == state, cancellationToken);
    }
}
