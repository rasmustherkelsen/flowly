using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.Repositories;

namespace SimpleTransit.MessageInfrastructure.MessageHandler;

internal class CreateRecurringJobStateHandler(IJobStateRepository jobStateRepository) : IMessageHandler<CreateRecurringJobState>
{
    public async Task Handle(IMessageContext<CreateRecurringJobState> messageContext)
        => await jobStateRepository.CreateRecurringJobState(messageContext.Message);
}