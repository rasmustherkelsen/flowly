using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Logging;

namespace Flowly.Jobs.MessageHandlers;

internal class StartRecurringJobMessageHandler(
    IJobStateRepository jobStateRepository,
    IMessageBusClient messageBusClient,
    ILogger<StartRecurringJobMessageHandler> logger) : MessageHandler<StartRecurringJobMessage>
{
    public override async Task Handle(IMessageContext<StartRecurringJobMessage> messageContext)
    {
        var jobs = await jobStateRepository.GetRecurringJobs();

        var recurringJob = jobs.SingleOrDefault(x => x.JobId == messageContext.Message.JobId);
        if (recurringJob == null)
        {
            logger.LogError($"Unknown recurring job id: '{messageContext.Message.JobId}'. Stopping processing");
            return;
        }
        
        var messageBusSender = await messageBusClient.CreateMessageBusSender(JobQueuesNames.RecurringJobs);
        await messageBusSender.SendEmptyMessage(new MessageProperties(recurringJob.JobId.ToString(), recurringJob.JobTypeName));
    }
}