using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.MessageHandlers;

internal class JobFailedHandler(IJobStateRepository jobStateRepository) : MessageHandler<FlowlysysJobFailedMessage>
{
    public override async Task Handle(IMessageContext<FlowlysysJobFailedMessage> jobFailed)
        => await jobStateRepository.UpdateJobFailed(jobFailed.Message);
}