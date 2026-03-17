using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.MessageHandlers;

internal class CreateRecurringJobStateHandler(IJobStateRepository jobStateRepository, ICustomJobStateRepository customJobStateRepository) : MessageHandlerBase<CreateRecurringJobState>
{
    public override async Task Handle(IMessageContext<CreateRecurringJobState> messageContext)
    {
        var newJobId = new JobId();

        await Task.WhenAll(
            jobStateRepository.CreateRecurringJobState(messageContext.Message, newJobId),
            customJobStateRepository.CreateCustomJobState(newJobId));
    }
        
}