using Flowly.Jobs.Model;

namespace Flowly.Jobs.Repositories;

internal interface IJobAliveStatusRepository
{
    Task CreateJobAliveStatus(JobId jobId, DateTimeOffset lastAliveTimeStamp);
    
    Task SetJobAliveStatus(JobId jobId, DateTimeOffset lastAliveTimeStamp);
}