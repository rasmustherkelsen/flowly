namespace Flowly.Jobs.Model;

internal class JobException(JobId jobId, Exception exception) : Exception($"Job {jobId} failed", exception)
{
    public JobId JobId { get; } = jobId;
}