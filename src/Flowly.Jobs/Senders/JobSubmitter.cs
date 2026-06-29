using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Jobs.Senders;

internal class JobSubmitter<TMessage>(
    IMessageBusClientRegistry clientRegistry,
    JobSubmitter<TMessage>.QueueSettings queueSettings,
    IMessageSender messageSender) : IJobSubmitter<TMessage> where TMessage : IJobMessage
{
    internal record QueueSettings(string QueueName, string ProviderName);

    public async Task<JobId> SubmitJob(TMessage message, CancellationToken cancellationToken = default)
    {
        var jobId = new JobId(Guid.NewGuid());
        var createJobState = new FlowlysysCreateJobStateMessage(jobId, message.JobTypeName, message.Description, DateTime.UtcNow);

        await messageSender.Send(createJobState, cancellationToken);

        var client = clientRegistry.GetClient(queueSettings.ProviderName);
        var messageBusSender = await client.CreateMessageBusSender(queueSettings.QueueName);

        await messageBusSender.SendMessage(message, new MessageProperties(jobId.InnerId.ToString(), string.Empty), cancellationToken);

        return jobId;
    }
}
