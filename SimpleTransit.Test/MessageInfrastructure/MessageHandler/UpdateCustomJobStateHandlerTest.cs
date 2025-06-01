using NSubstitute;
using SimpleTransit.MessageInfrastructure.MessageHandler;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.Repositories;
using SimpleTransit.Test.MessageInfrastructure.MessageHandler.Setup;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.MessageHandler;

public class UpdateCustomJobStateHandlerTest
{
    public class Handle
    {
        [Theory, AutoDataWithCustomization(typeof(SetupHandlerForTest))]
        internal async Task MustPassMessageToRepository(UpdateCustomJobStateHandler updateCustomJobStateHandler, IMessageContext<UpdateCustomJobState> updateCustomJobStateMessage, IJobStateRepository jobStateRepository)
        {
            await updateCustomJobStateHandler.Handle(updateCustomJobStateMessage);
            
            await jobStateRepository.Received(1).UpdateJobCustomState(Arg.Is<UpdateCustomJobState>(x => x == updateCustomJobStateMessage.Message));
        }
    }
}