using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;

namespace Flowly.Jobs.Repositories;

internal interface ICustomJobStateRepository
{
    Task CreateCustomJobState(JobId jobId);
    
    Task UpdateJobCustomState(UpdateCustomJobState updateCustomJobState);
}