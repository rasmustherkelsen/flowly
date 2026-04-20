using Flowly.Jobs.MessageHandlers;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.MessageHandlers;

public class UpdateCustomJobStateHandlerTests
{
    public class Handle
    {
        [Fact]
        public async Task CallsUpdateJobCustomStateOnRepositoryWithMessage()
        {
            var customJobStateRepository = new FakeCustomJobStateRepository();
            var updateCustomJobStateHandler = new UpdateCustomJobStateHandler(customJobStateRepository);
            var message = new UpdateCustomJobState(new JobId(), new { Progress = 50 });

            await updateCustomJobStateHandler.Handle(new TestMessageContext<UpdateCustomJobState>(message));

            Assert.Single(customJobStateRepository.Updates);
            Assert.Same(message, customJobStateRepository.Updates[0]);
        }
    }
}
