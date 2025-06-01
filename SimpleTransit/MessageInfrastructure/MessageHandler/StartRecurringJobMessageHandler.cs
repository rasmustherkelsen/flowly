using Microsoft.Extensions.Logging;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.Repositories;

namespace SimpleTransit.MessageInfrastructure.MessageHandler;

internal class StartRecurringJobMessageHandler(
    IJobStateRepository jobStateRepository,
    IMessageSender messageSender,
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

        await messageSender.SendMessage(QueuesNames.RecurringJobs, recurringJob.JobId, recurringJob.JobTypeName);
    }
}