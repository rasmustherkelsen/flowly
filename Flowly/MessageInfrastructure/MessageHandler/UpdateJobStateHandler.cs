using Flowly.MessageInfrastructure.Messages;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.Repositories;

namespace Flowly.MessageInfrastructure.MessageHandler;

internal class UpdateJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<UpdateJobState>
{
    public async Task Handle(IMessageContext<UpdateJobState> messageContext)
        => await jobStateRepository.UpdateJobState(messageContext.Message);
}