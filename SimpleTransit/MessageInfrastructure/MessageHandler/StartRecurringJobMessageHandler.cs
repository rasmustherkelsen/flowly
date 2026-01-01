using Microsoft.Extensions.Logging;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.MessagingAbstractions;
using SimpleTransit.Repositories;

namespace SimpleTransit.MessageInfrastructure.MessageHandler;

internal class StartRecurringJobMessageHandler(
    IJobStateRepository jobStateRepository,
    IMessageBusClient messageBusClient,
    ILogger<StartRecurringJobMessageHandler> logger) : IMessageHandler<StartRecurringJobMessage>
{
    public async Task Handle(IMessageContext<StartRecurringJobMessage> messageContext)
    {
        var jobs = await jobStateRepository.GetRecurringJobs();

        var recurringJob = jobs.SingleOrDefault(x => x.JobId == messageContext.Message.JobId);
        if (recurringJob == null)
        {
            logger.LogError($"Unknown recurring job id: '{messageContext.Message.JobId}'. Stopping processing");
            return;
        }
        
        var messageBusSender = messageBusClient.CreateMessageBusSender(QueuesNames.RecurringJobs);
        await messageBusSender.SendEmptyMessage(new MessageProperties(recurringJob.JobId.ToString(), recurringJob.JobTypeName));
    }
}