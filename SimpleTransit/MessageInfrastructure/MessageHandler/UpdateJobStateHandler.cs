using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.Repositories;

namespace SimpleTransit.MessageInfrastructure.MessageHandler;

internal class UpdateJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<UpdateJobState>
{
    public async Task Handle(IMessageContext<UpdateJobState> messageContext)
        => await jobStateRepository.UpdateJobState(messageContext.Message);
}