using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.MessageHandlers;

internal class CreateJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<CreateJobState>
{
    public async Task Handle(IMessageContext<CreateJobState> messageContext)
        => await jobStateRepository.CreateJobState(messageContext.Message);
}