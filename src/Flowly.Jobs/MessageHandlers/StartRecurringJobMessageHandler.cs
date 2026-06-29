using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Logging;

namespace Flowly.Jobs.MessageHandlers;

internal class StartRecurringJobMessageHandler(
    IJobStateRepository jobStateRepository,
    IMessageBusClientRegistry clientRegistry,
    ITopologyNameResolver topologyNameResolver,
    ILogger<StartRecurringJobMessageHandler> logger) : MessageHandler<FlowlysysStartRecurringJobMessage>
{
    public override async Task Handle(IMessageContext<FlowlysysStartRecurringJobMessage> messageContext)
    {
        var jobs = await jobStateRepository.GetRecurringJobs();

        var recurringJob = jobs.SingleOrDefault(x => x.JobId == messageContext.Message.JobId);
        if (recurringJob == null)
        {
            logger.LogError("Unknown recurring job id: '{JobId}'. Stopping processing", messageContext.Message.JobId);
            return;
        }

        var client = clientRegistry.GetClient(clientRegistry.PrimaryProviderName);
        var recurringJobsQueueName = topologyNameResolver.ResolveQueueName<FlowlysysRecurringJobsMessage>();
        var messageBusSender = await client.CreateMessageBusSender(recurringJobsQueueName);
        await messageBusSender.SendEmptyMessage(new MessageProperties(recurringJob.JobId.ToString(), recurringJob.JobTypeName));
    }
}