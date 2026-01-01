using Flowly.MessageInfrastructure.Messages;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.Repositories;

namespace Flowly.MessageInfrastructure.MessageHandler;

internal class CreateRecurringJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<CreateRecurringJobState>
{
    public async Task Handle(IMessageContext<CreateRecurringJobState> messageContext)
        => await jobStateRepository.CreateRecurringJobState(messageContext.Message);
}