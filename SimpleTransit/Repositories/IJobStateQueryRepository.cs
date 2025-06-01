namespace SimpleTransit.Repositories;

public interface IJobStateQueryRepository
{
    Task<bool> Exists(Guid jobId, bool? recurring = null);

    Task<IReadOnlyCollection<JobInformation>> Query(JobQuery? jobQuery = null);
}