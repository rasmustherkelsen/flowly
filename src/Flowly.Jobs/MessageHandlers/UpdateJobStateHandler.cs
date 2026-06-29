using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.MessageHandlers;

internal class UpdateJobStateHandler(IJobStateRepository jobStateRepository) : MessageHandler<FlowlysysUpdateJobStateMessage>
{
    public override async Task Handle(IMessageContext<FlowlysysUpdateJobStateMessage> messageContext)
        => await jobStateRepository.UpdateJobState(messageContext.Message);
}