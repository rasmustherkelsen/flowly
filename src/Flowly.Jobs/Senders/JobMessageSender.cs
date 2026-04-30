using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Senders;

internal class JobMessageSender(IServiceProvider serviceProvider, IMessageSender messageSender) : IJobMessageSender
{
    public async Task<JobId> QueueJob<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IJobMessage
    {
        var messageSubmitter = serviceProvider.GetRequiredService<IJobSubmitter<TMessage>>();
        return await messageSubmitter.SubmitJob(message, cancellationToken);
    }

    public async Task StartRecurringJob(Guid jobId)
    {
        await messageSender.Send(new StartRecurringJobMessage(jobId));
    }
}