using NSubstitute;
using SimpleTransit.MessageInfrastructure.MessageHandler;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.Repositories;
using SimpleTransit.Test.MessageInfrastructure.MessageHandler.Setup;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.MessageHandler;

public class CreateJobStateHandlerTest
{
    public class Handle
    {
        [Theory, AutoDataWithCustomization(typeof(SetupHandlerForTest))]
        internal async Task MustPassMessageToRepository(CreateJobStateHandler createJobStateHandler, IJobStateRepository jobStateRepository, IMessageContext<CreateJobState> messageContext)
        {
            await createJobStateHandler.Handle(messageContext);

            await jobStateRepository.Received(1).CreateJobState(Arg.Is<CreateJobState>(x => x == messageContext.Message));
        }
    }
}