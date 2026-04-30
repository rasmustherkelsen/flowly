using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.Tests.Fakes;

internal class FakeCustomJobStateRepository : ICustomJobStateRepository
{
    public List<JobId> CreatedFor { get; } = [];
    public List<UpdateCustomJobState> Updates { get; } = [];

    public Task CreateCustomJobState(JobId jobId)
    {
        CreatedFor.Add(jobId);
        return Task.CompletedTask;
    }

    public Task UpdateJobCustomState(UpdateCustomJobState updateCustomJobState)
    {
        Updates.Add(updateCustomJobState);
        return Task.CompletedTask;
    }
}
