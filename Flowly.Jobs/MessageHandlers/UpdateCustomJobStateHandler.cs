using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.MessageHandlers;

internal class UpdateCustomJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<UpdateCustomJobState>
{
    public async Task Handle(IMessageContext<UpdateCustomJobState> messageContext)
        => await jobStateRepository.UpdateJobCustomState(messageContext.Message);
}