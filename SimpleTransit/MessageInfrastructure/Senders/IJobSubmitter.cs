using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.Senders;

internal interface IJobSubmitter<in TMessage> where TMessage : IJobMessage
{
    Task<JobId> SubmitJob(TMessage message, CancellationToken cancellationToken = default);
}