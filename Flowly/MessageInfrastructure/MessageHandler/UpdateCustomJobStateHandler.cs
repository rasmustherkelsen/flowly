using Flowly.MessageInfrastructure.Messages;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.Repositories;

namespace Flowly.MessageInfrastructure.MessageHandler;

internal class UpdateCustomJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<UpdateCustomJobState>
{
    public async Task Handle(IMessageContext<UpdateCustomJobState> messageContext)
        => await jobStateRepository.UpdateJobCustomState(messageContext.Message);
}