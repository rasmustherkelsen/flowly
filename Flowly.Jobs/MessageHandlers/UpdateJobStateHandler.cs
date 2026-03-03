using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.MessageHandlers;

[QueueName(JobQueuesNames.UpdateJobState)]
internal class UpdateJobStateHandler(IJobStateRepository jobStateRepository) : MessageHandlerBase<UpdateJobState>
{
    public override async Task Handle(IMessageContext<UpdateJobState> messageContext)
        => await jobStateRepository.UpdateJobState(messageContext.Message);
}