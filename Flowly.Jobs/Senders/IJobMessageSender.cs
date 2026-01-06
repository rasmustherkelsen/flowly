using Flowly.Jobs.Model;

namespace Flowly.Jobs.Senders;

public interface IJobMessageSender
{
    Task<JobId> QueueJob<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IJobMessage;

    Task StartRecurringJob(Guid jobId);
}