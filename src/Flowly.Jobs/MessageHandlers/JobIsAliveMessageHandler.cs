using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.MessageHandlers;

internal class JobIsAliveMessageHandler(IJobAliveStatusRepository jobAliveStatusRepository) : MessageHandler<FlowlysysJobIsAliveMessage>
{
    public override async Task Handle(IMessageContext<FlowlysysJobIsAliveMessage> messageContext)
    {
        await jobAliveStatusRepository.SetJobAliveStatus(messageContext.Message.JobId, messageContext.Message.TimeStamp);
    }
}