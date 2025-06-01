namespace SimpleTransit.MessageInfrastructure.Model;

internal class JobException(Guid jobId, Exception exception) : Exception($"Job {jobId} failed", exception)
{
    public Guid JobId { get; } = jobId;
}