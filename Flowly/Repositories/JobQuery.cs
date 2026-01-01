namespace Flowly.Repositories;

public class JobQuery
{
    public Guid? JobId { get; set; }

    public string? JobType { get; set; }

    public static JobQuery ByJobType(string jobType) => new() { JobType = jobType };

    public static JobQuery ById(Guid jobId) => new() { JobId = jobId };
}