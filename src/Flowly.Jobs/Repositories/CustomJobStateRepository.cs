using System.Text.Json;
using Flowly.Jobs.DatabaseModel;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.Repositories;

internal class CustomJobStateRepository(IDbContextFactory<JobStateDataContext> jobStateDataContextFactory) : ICustomJobStateRepository
{
    public async Task CreateCustomJobState(JobId jobId)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();
        
        var customJobState = new CustomJobState
        {
            JobIdentifier = jobId.InnerId,
            CustomState = null
        };
        
        context.CustomJobStates.Add(customJobState);
        await context.SaveChangesAsync();
    }
    
    public async Task UpdateJobCustomState(FlowlysysUpdateJobCustomStateMessage flowlysysUpdateJobCustomStateMessage)
    {
        await using var context = await jobStateDataContextFactory.CreateDbContextAsync();

        var jsonState = JsonSerializer.Serialize(flowlysysUpdateJobCustomStateMessage.CustomState);

        await context.CustomJobStates
            .Where(x => x.JobIdentifier == flowlysysUpdateJobCustomStateMessage.JobId.InnerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomState, jsonState));
    }
}