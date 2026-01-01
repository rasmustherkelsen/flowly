using Flowly.MessageInfrastructure.Messages;
using Flowly.MessageInfrastructure.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Senders;

internal class MessageSender(IServiceProvider serviceProvider) : IMessageSender
{
    public async Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default)
    {
        var messageSubmitter = serviceProvider.GetRequiredService<IMessageSubmitter<TMessage>>();
        await messageSubmitter.Submit(message, cancellationToken);
    }

    public async Task<JobId> QueueJob<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IJobMessage
    {
        var messageSubmitter = serviceProvider.GetRequiredService<IJobSubmitter<TMessage>>();
        return await messageSubmitter.SubmitJob(message, cancellationToken);
    }

    public async Task StartRecurringJob(Guid jobId)
        => await Send(new StartRecurringJobMessage(jobId));
}