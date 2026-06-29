using Flowly.Jobs.MessageHandlers;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.MessageHandlers;

public class JobFailedHandlerTests
{
    public class Handle
    {
        [Fact]
        public async Task CallsUpdateJobFailedOnRepositoryWithMessage()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var jobFailedHandler = new JobFailedHandler(jobStateRepository);
            var message = new FlowlysysJobFailedMessage(new JobId(), "something broke", DateTime.UtcNow);

            await jobFailedHandler.Handle(new TestMessageContext<FlowlysysJobFailedMessage>(message));

            Assert.Single(jobStateRepository.FailedJobs);
            Assert.Same(message, jobStateRepository.FailedJobs[0]);
        }
    }
}
