namespace Flowly.Jobs.Repositories;

internal interface IJobStateCountReader
{
    Task<long> CountJobsInState(JobState state, CancellationToken cancellationToken);
}
