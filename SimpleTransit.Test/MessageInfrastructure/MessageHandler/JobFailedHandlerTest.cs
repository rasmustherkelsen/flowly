using NSubstitute;
using SimpleTransit.MessageInfrastructure.MessageHandler;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.Repositories;
using SimpleTransit.Test.MessageInfrastructure.MessageHandler.Setup;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.MessageHandler;

public class JobFailedHandlerTest
{
    public class Handle
    {
        [Theory, AutoDataWithCustomization(typeof(SetupHandlerForTest))]
        internal async Task MustPassMessageToRepository(JobFailedHandler jobFailedHandler, IMessageContext<JobFailed> jobFailed, IJobStateRepository jobStateRepository)
        {
            await jobFailedHandler.Handle(jobFailed);
            
            await jobStateRepository.Received(1).UpdateJobFailed(Arg.Is<JobFailed>(x => x == jobFailed.Message));
        }
    }
}