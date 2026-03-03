using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.MessageHandlers;

[QueueName(JobQueuesNames.CreateJobState)]
internal class CreateJobStateHandler(
    IJobStateRepository jobStateRepository, 
    IJobAliveStatusRepository jobAliveStatusRepository,
    ICustomJobStateRepository customJobStateRepository) : MessageHandlerBase<CreateJobState>
{
    public override async Task Handle(IMessageContext<CreateJobState> messageContext)
        => await Task.WhenAll(
            jobStateRepository.CreateJobState(messageContext.Message),
            jobAliveStatusRepository.CreateJobAliveStatus(messageContext.Message.JobId, messageContext.Message.TimeStamp),
            customJobStateRepository.CreateCustomJobState(messageContext.Message.JobId));
}
