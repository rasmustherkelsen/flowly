using Azure.Messaging.ServiceBus;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using System.Text.Json;

namespace SimpleTransit.MessageInfrastructure.Senders;

internal class JobSubmitter<TMessage>(IServiceBusClient serviceBusClient, JobSubmitter<TMessage>.QueueSettings queueSettings, IMessageSender messageSender) : IJobSubmitter<TMessage> where TMessage : IJobMessage
{
    internal record QueueSettings(string QueueName);

    public async Task<JobId> SubmitJob(TMessage message, CancellationToken cancellationToken = default)
    {
        var jobId = new JobId(Guid.NewGuid());
        var createJobState = new CreateJobState(jobId.InnerId, message.JobTypeName, message.Description, DateTime.UtcNow);

        await messageSender.Send(createJobState, cancellationToken);

        var serviceBusSender = serviceBusClient.GetServiceBusSender(queueSettings.QueueName);

        await serviceBusSender.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(message)) { MessageId = jobId.ToString() }, cancellationToken);

        return jobId;
    }
}
