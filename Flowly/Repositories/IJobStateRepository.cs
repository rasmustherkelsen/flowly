using Flowly.MessageInfrastructure.Messages;
using Flowly.MessageInfrastructure.Model;

namespace Flowly.Repositories;

internal interface IJobStateRepository
{
    Task CreateJobState(CreateJobState createJobState);

    Task CreateRecurringJobState(CreateRecurringJobState createRecurringJobState);

    Task UpdateJobState(UpdateJobState updateJobState);

    Task UpdateJobFailed(JobFailed jobFailed);

    Task UpdateJobCustomState(UpdateCustomJobState updateCustomJobState);

    Task RemoveJobsOlderThan(TimeSpan age);
    
    Task<IReadOnlyCollection<RecurringJob>> GetRecurringJobs();
    
    Task FailUncompletedJobsOlderThan(TimeSpan age);
}
