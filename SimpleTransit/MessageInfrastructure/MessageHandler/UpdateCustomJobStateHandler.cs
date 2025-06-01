using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.Repositories;

namespace SimpleTransit.MessageInfrastructure.MessageHandler;

internal class UpdateCustomJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<UpdateCustomJobState>
{
    public async Task Handle(IMessageContext<UpdateCustomJobState> messageContext)
        => await jobStateRepository.UpdateJobCustomState(messageContext.Message);
}