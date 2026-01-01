using Flowly.MessageInfrastructure.Model;

namespace Flowly.MessageInfrastructure.Senders;

internal interface IJobSubmitter<in TMessage> where TMessage : IJobMessage
{
    Task<JobId> SubmitJob(TMessage message, CancellationToken cancellationToken = default);
}