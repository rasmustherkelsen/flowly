using Flowly.Jobs.MessageHandlers;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.MessageHandlers;

public class UpdateJobStateHandlerTests
{
    public class Handle
    {
        [Fact]
        public async Task CallsUpdateJobStateOnRepositoryWithMessage()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var updateJobStateHandler = new UpdateJobStateHandler(jobStateRepository);
            var message = new FlowlysysUpdateJobStateMessage(new JobId(), JobState.Started, DateTime.UtcNow);

            await updateJobStateHandler.Handle(new TestMessageContext<FlowlysysUpdateJobStateMessage>(message));

            Assert.Single(jobStateRepository.UpdatedJobStates);
            Assert.Same(message, jobStateRepository.UpdatedJobStates[0]);
        }

        [Fact]
        public async Task PassesMessageThroughForCompletedState()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var updateJobStateHandler = new UpdateJobStateHandler(jobStateRepository);
            var message = new FlowlysysUpdateJobStateMessage(new JobId(), JobState.Completed, DateTime.UtcNow, RetryAttempt: 2);

            await updateJobStateHandler.Handle(new TestMessageContext<FlowlysysUpdateJobStateMessage>(message));

            Assert.Equal(2, jobStateRepository.UpdatedJobStates[0].RetryAttempt);
            Assert.Equal(JobState.Completed, jobStateRepository.UpdatedJobStates[0].JobState);
        }
    }
}
