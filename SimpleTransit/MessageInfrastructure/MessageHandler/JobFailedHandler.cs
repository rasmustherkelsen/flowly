using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.Repositories;

namespace SimpleTransit.MessageInfrastructure.MessageHandler;

internal class JobFailedHandler(IJobStateRepository jobStateRepository) : IMessageHandler<JobFailed>
{
    public async Task Handle(IMessageContext<JobFailed> jobFailed)
        => await jobStateRepository.UpdateJobFailed(jobFailed.Message);
}