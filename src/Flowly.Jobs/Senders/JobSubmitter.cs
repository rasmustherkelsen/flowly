using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.SendSteps;
using Flowly.Transport;

namespace Flowly.Jobs.Senders;

internal class JobSubmitter<TMessage>(
    IMessageBusClientRegistry clientRegistry,
    string queueName,
    string providerName,
    IMessageSender messageSender,
    ISendStepRunner sendStepRunner) : IJobSubmitter<TMessage> where TMessage : IJobMessage
{
    public async Task<JobId> SubmitJob(TMessage message, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string>? customProperties = null)
    {
        CustomMessageProperties.Validate(customProperties, nameof(customProperties));

        JobId? jobId = null;

        await sendStepRunner.Run(message, cancellationToken, async () => jobId = await SubmitJobCore(message, cancellationToken, customProperties));

        return jobId ?? throw new InvalidOperationException(
            $"A send step prevented the job '{typeof(TMessage).Name}' from being queued, so there is no job id to return. " +
            "Throw from the send step to reject the job instead of returning without calling next.");
    }

    private async Task<JobId> SubmitJobCore(TMessage message, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? customProperties)
    {
        var jobId = new JobId(Guid.NewGuid());
        var createJobState = new FlowlysysCreateJobStateMessage(jobId, message.JobTypeName, message.Description, DateTime.UtcNow);

        await messageSender.Send(createJobState, cancellationToken);

        var client = clientRegistry.GetClient(providerName);
        var messageBusSender = await client.CreateMessageBusSender(queueName);

        await messageBusSender.SendMessage(message, new MessageProperties(jobId.InnerId.ToString(), string.Empty) { CustomProperties = customProperties ?? CustomMessageProperties.Empty },
            cancellationToken);

        return jobId;
    }
}
