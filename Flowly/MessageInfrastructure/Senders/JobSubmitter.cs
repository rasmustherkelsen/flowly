using Flowly.MessageInfrastructure.Messages;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Senders;

internal class JobSubmitter<TMessage>(
    IMessageBusClient messageBusClient,
    JobSubmitter<TMessage>.QueueSettings queueSettings,
    IMessageSender messageSender) : IJobSubmitter<TMessage> where TMessage : IJobMessage
{
    internal record QueueSettings(string QueueName);

    public async Task<JobId> SubmitJob(TMessage message, CancellationToken cancellationToken = default)
    {
        var jobId = new JobId(Guid.NewGuid());
        var createJobState = new CreateJobState(jobId.InnerId, message.JobTypeName, message.Description, DateTime.UtcNow);

        await messageSender.Send(createJobState, cancellationToken);

        var messageBusSender = messageBusClient.CreateMessageBusSender(queueSettings.QueueName);

        await messageBusSender.SendMessage(message, new MessageProperties(jobId.InnerId.ToString(), string.Empty), cancellationToken);

        return jobId;
    }
}