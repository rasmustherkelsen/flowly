using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.MessageHandlers;

internal class UpdateCustomJobStateHandler(ICustomJobStateRepository customJobStateRepository) : MessageHandler<FlowlysysUpdateJobCustomStateMessage>
{
    public override async Task Handle(IMessageContext<FlowlysysUpdateJobCustomStateMessage> messageContext)
        => await customJobStateRepository.UpdateJobCustomState(messageContext.Message);
}