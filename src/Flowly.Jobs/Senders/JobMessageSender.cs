using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Senders;

internal class JobMessageSender(IServiceProvider serviceProvider, IMessageSender messageSender) : IJobMessageSender
{
    public Task<JobId> QueueJob<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? customProperties = null) where TMessage : IJobMessage
        => serviceProvider.GetRequiredService<IJobSubmitter<TMessage>>().SubmitJob(message, cancellationToken, customProperties);

    public async Task StartRecurringJob(Guid jobId)
    {
        await messageSender.Send(new FlowlysysStartRecurringJobMessage(jobId));
    }
}