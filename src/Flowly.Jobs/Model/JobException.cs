using Flowly.MessageInfrastructure;

namespace Flowly.Jobs.Model;

internal class JobException(JobId jobId, Exception exception) : Exception($"Job {jobId} failed", exception), IHandlerExceptionWrapper
{
    public JobId JobId { get; } = jobId;

    public Exception WrappedException { get; } = exception;
}