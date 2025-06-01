using NSubstitute;
using SimpleTransit.MessageInfrastructure.MessageHandler;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.Repositories;
using SimpleTransit.Test.MessageInfrastructure.MessageHandler.Setup;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.MessageHandler;

public class CreateRecurringJobStateHandlerTest
{
    public class Handle
    {
        [Theory, AutoDataWithCustomization(typeof(SetupHandlerForTest))]
        internal async Task MustPassMessageToRepository(CreateRecurringJobStateHandler createRecurringJobStateHandler, IJobStateRepository jobStateRepository, IMessageContext<CreateRecurringJobState> messageContext)
        {
            await createRecurringJobStateHandler.Handle(messageContext);

            await jobStateRepository.Received(1).CreateRecurringJobState(Arg.Is<CreateRecurringJobState>(x => x == messageContext.Message));
        }
    }
}