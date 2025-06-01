using NSubstitute;
using SimpleTransit.MessageInfrastructure.MessageHandler;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.Repositories;
using SimpleTransit.Test.MessageInfrastructure.MessageHandler.Setup;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.MessageHandler;

public class UpdateJobStateHandlerTest
{
    public class Handle
    {
        [Theory, AutoDataWithCustomization(typeof(SetupHandlerForTest))]
        internal async Task MustPassMessageToRepository(UpdateJobStateHandler updateJobStateHandler, IMessageContext<UpdateJobState> updateCustomJobStateMessage, IJobStateRepository jobStateRepository)
        {
            await updateJobStateHandler.Handle(updateCustomJobStateMessage);
            
            await jobStateRepository.Received(1).UpdateJobState(Arg.Is<UpdateJobState>(x => x == updateCustomJobStateMessage.Message));
        }
    }
}