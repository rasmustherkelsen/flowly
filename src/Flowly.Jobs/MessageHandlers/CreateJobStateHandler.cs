using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.MessageHandlers;

internal class CreateJobStateHandler(
    IJobStateRepository jobStateRepository,
    IJobAliveStatusRepository jobAliveStatusRepository,
    ICustomJobStateRepository customJobStateRepository) : MessageHandler<CreateJobState>
{
    public override async Task Handle(IMessageContext<CreateJobState> messageContext)
    {
        await Task.WhenAll(
            jobStateRepository.CreateJobState(messageContext.Message),
            jobAliveStatusRepository.CreateJobAliveStatus(messageContext.Message.JobId, messageContext.Message.TimeStamp),
            customJobStateRepository.CreateCustomJobState(messageContext.Message.JobId));
    }
}