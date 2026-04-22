using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;

namespace Flowly.Jobs.MessageHandlers;

internal class JobIsAliveMessageHandler(IJobAliveStatusRepository jobAliveStatusRepository) : MessageHandler<JobIsAlive>
{
    public override async Task Handle(IMessageContext<JobIsAlive> messageContext)
        => await jobAliveStatusRepository.SetJobAliveStatus(messageContext.Message.JobId, messageContext.Message.TimeStamp);
}