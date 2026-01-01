using Flowly.MessageInfrastructure.Messages;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.Repositories;

namespace Flowly.MessageInfrastructure.MessageHandler;

internal class CreateJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<CreateJobState>
{
    public async Task Handle(IMessageContext<CreateJobState> messageContext)
        => await jobStateRepository.CreateJobState(messageContext.Message);
}