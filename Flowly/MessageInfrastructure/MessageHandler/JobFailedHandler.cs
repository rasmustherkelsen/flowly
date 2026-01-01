using Flowly.MessageInfrastructure.Messages;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.Repositories;

namespace Flowly.MessageInfrastructure.MessageHandler;

internal class JobFailedHandler(IJobStateRepository jobStateRepository) : IMessageHandler<JobFailed>
{
    public async Task Handle(IMessageContext<JobFailed> jobFailed)
        => await jobStateRepository.UpdateJobFailed(jobFailed.Message);
}