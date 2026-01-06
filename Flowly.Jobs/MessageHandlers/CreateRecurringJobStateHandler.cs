using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.MessageHandlers;

internal class CreateRecurringJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<CreateRecurringJobState>
{
    public async Task Handle(IMessageContext<CreateRecurringJobState> messageContext)
        => await jobStateRepository.CreateRecurringJobState(messageContext.Message);
}