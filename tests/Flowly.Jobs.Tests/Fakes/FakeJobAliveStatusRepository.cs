using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.Tests.Fakes;

internal class FakeJobAliveStatusRepository : IJobAliveStatusRepository
{
    public List<(JobId JobId, DateTimeOffset TimeStamp)> CreatedStatuses { get; } = [];
    public List<(JobId JobId, DateTimeOffset TimeStamp)> SetStatuses { get; } = [];

    public Task CreateJobAliveStatus(JobId jobId, DateTimeOffset lastAliveTimeStamp)
    {
        CreatedStatuses.Add((jobId, lastAliveTimeStamp));
        return Task.CompletedTask;
    }

    public Task SetJobAliveStatus(JobId jobId, DateTimeOffset lastAliveTimeStamp)
    {
        SetStatuses.Add((jobId, lastAliveTimeStamp));
        return Task.CompletedTask;
    }
}
