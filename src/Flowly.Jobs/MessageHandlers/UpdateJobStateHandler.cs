using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.MessageHandlers;

internal class UpdateJobStateHandler(IJobStateRepository jobStateRepository) : MessageHandler<UpdateJobState>
{
    public override async Task Handle(IMessageContext<UpdateJobState> messageContext)
        => await jobStateRepository.UpdateJobState(messageContext.Message);
}