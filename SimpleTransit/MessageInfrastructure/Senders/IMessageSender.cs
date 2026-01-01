using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.Senders;

public interface IMessageSender
{
    Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default);
    
    Task<JobId> QueueJob<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IJobMessage;

    Task StartRecurringJob(Guid jobId);
}