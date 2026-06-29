using Flowly.Jobs.MessageHandlers;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.MessageHandlers;

public class JobIsAliveMessageHandlerTests
{
    public class Handle
    {
        [Fact]
        public async Task SetsJobAliveStatusWithMessageJobIdAndTimestamp()
        {
            var jobAliveStatusRepository = new FakeJobAliveStatusRepository();
            var jobIsAliveMessageHandler = new JobIsAliveMessageHandler(jobAliveStatusRepository);
            var jobId = new JobId();
            var timestamp = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var message = new FlowlysysJobIsAliveMessage(jobId, timestamp);

            await jobIsAliveMessageHandler.Handle(new TestMessageContext<FlowlysysJobIsAliveMessage>(message));

            Assert.Single(jobAliveStatusRepository.SetStatuses);
            Assert.Equal(jobId, jobAliveStatusRepository.SetStatuses[0].JobId);
            Assert.Equal(timestamp, jobAliveStatusRepository.SetStatuses[0].TimeStamp);
        }

        [Fact]
        public async Task DoesNotCreateNewAliveStatus()
        {
            var jobAliveStatusRepository = new FakeJobAliveStatusRepository();
            var jobIsAliveMessageHandler = new JobIsAliveMessageHandler(jobAliveStatusRepository);
            var message = new FlowlysysJobIsAliveMessage(new JobId(), DateTimeOffset.UtcNow);

            await jobIsAliveMessageHandler.Handle(new TestMessageContext<FlowlysysJobIsAliveMessage>(message));

            Assert.Empty(jobAliveStatusRepository.CreatedStatuses);
        }
    }
}
