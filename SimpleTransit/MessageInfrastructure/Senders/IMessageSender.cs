using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.Senders;

public interface IMessageSender
{
    Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default);
    
    Task<JobId> SendJob<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IJobMessage;
    
    Task SendMessage(string queueName, Guid messageId, string sessionId);

    Task StartRecurringJob(Guid jobId);
}