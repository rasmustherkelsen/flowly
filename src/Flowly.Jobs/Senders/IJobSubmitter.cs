using Flowly.Jobs.Model;

namespace Flowly.Jobs.Senders;

internal interface IJobSubmitter<in TMessage> where TMessage : IJobMessage
{
    Task<JobId> SubmitJob(TMessage message, CancellationToken cancellationToken = default);
}