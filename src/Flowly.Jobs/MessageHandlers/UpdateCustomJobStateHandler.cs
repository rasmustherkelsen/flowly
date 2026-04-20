using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.MessageHandlers;

internal class UpdateCustomJobStateHandler(ICustomJobStateRepository customJobStateRepository) : MessageHandler<UpdateCustomJobState>
{
    public override async Task Handle(IMessageContext<UpdateCustomJobState> messageContext)
        => await customJobStateRepository.UpdateJobCustomState(messageContext.Message);
}