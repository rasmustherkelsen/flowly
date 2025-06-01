using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.Repositories;

namespace SimpleTransit.MessageInfrastructure.MessageHandler;

internal class CreateJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<CreateJobState>
{
    public async Task Handle(IMessageContext<CreateJobState> messageContext)
        => await jobStateRepository.CreateJobState(messageContext.Message);
}