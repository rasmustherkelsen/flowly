using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.MessageHandlers;

internal class JobFailedHandler(IJobStateRepository jobStateRepository) : MessageHandler<JobFailed>
{
    public override async Task Handle(IMessageContext<JobFailed> jobFailed)
        => await jobStateRepository.UpdateJobFailed(jobFailed.Message);
}