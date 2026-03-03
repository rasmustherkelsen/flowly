using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.MessageHandlers;

[QueueName(JobQueuesNames.UpdateJobCustomState)]
internal class UpdateCustomJobStateHandler(ICustomJobStateRepository customJobStateRepository) : MessageHandlerBase<UpdateCustomJobState>
{
    public override async Task Handle(IMessageContext<UpdateCustomJobState> messageContext)
        => await customJobStateRepository.UpdateJobCustomState(messageContext.Message);
}