using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.MessageHandlers;

[QueueName(JobQueuesNames.JobFailed)]
internal class JobFailedHandler(IJobStateRepository jobStateRepository) : MessageHandlerBase<JobFailed>
{
    public override async Task Handle(IMessageContext<JobFailed> jobFailed)
        => await jobStateRepository.UpdateJobFailed(jobFailed.Message);
}