using Flowly.Jobs.MessageHandlers;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.MessageHandlers;

public class CreateJobStateHandlerTests
{
    public class Handle
    {
        [Fact]
        public async Task CreatesJobState_JobAliveStatus_AndCustomJobState()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var jobAliveStatusRepository = new FakeJobAliveStatusRepository();
            var customJobStateRepository = new FakeCustomJobStateRepository();
            var createJobStateHandler = new CreateJobStateHandler(jobStateRepository, jobAliveStatusRepository, customJobStateRepository);
            var jobId = new JobId();
            var message = new CreateJobState(jobId, "JobType", "Description", DateTime.UtcNow);

            await createJobStateHandler.Handle(new TestMessageContext<CreateJobState>(message));

            Assert.Single(jobStateRepository.CreatedJobStates);
            Assert.Same(message, jobStateRepository.CreatedJobStates[0]);
        }

        [Fact]
        public async Task SetsAliveStatusWithMessageJobIdAndTimestamp()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var jobAliveStatusRepository = new FakeJobAliveStatusRepository();
            var customJobStateRepository = new FakeCustomJobStateRepository();
            var createJobStateHandler = new CreateJobStateHandler(jobStateRepository, jobAliveStatusRepository, customJobStateRepository);
            var jobId = new JobId();
            var timestamp = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var message = new CreateJobState(jobId, "JobType", "Description", timestamp);

            await createJobStateHandler.Handle(new TestMessageContext<CreateJobState>(message));

            Assert.Single(jobAliveStatusRepository.CreatedStatuses);
            Assert.Equal(jobId, jobAliveStatusRepository.CreatedStatuses[0].JobId);
            Assert.Equal(timestamp, jobAliveStatusRepository.CreatedStatuses[0].TimeStamp);
        }

        [Fact]
        public async Task CreatesCustomJobStateForSameJobId()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var jobAliveStatusRepository = new FakeJobAliveStatusRepository();
            var customJobStateRepository = new FakeCustomJobStateRepository();
            var createJobStateHandler = new CreateJobStateHandler(jobStateRepository, jobAliveStatusRepository, customJobStateRepository);
            var jobId = new JobId();
            var message = new CreateJobState(jobId, "JobType", "Description", DateTime.UtcNow);

            await createJobStateHandler.Handle(new TestMessageContext<CreateJobState>(message));

            Assert.Single(customJobStateRepository.CreatedFor);
            Assert.Equal(jobId, customJobStateRepository.CreatedFor[0]);
        }
    }
}
