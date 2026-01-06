using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.MessageHandlers;

internal class JobFailedHandler(IJobStateRepository jobStateRepository) : IMessageHandler<JobFailed>
{
    public async Task Handle(IMessageContext<JobFailed> jobFailed)
        => await jobStateRepository.UpdateJobFailed(jobFailed.Message);
}