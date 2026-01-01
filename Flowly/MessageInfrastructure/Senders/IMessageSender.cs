using Flowly.MessageInfrastructure.Model;

namespace Flowly.MessageInfrastructure.Senders;

public interface IMessageSender
{
    Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default);
    
    Task<JobId> QueueJob<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IJobMessage;

    Task StartRecurringJob(Guid jobId);
}