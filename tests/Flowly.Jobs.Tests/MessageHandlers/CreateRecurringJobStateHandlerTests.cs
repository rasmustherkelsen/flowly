using Flowly.Jobs.MessageHandlers;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.MessageHandlers;

public class CreateRecurringJobStateHandlerTests
{
    public class Handle
    {
        [Fact]
        public async Task CreatesRecurringJobStateAndCustomJobStateWithSameNewJobId()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var customJobStateRepository = new FakeCustomJobStateRepository();
            var createRecurringJobStateHandler = new CreateRecurringJobStateHandler(jobStateRepository, customJobStateRepository);
            var message = new CreateRecurringJobState("JobType", "Description", DateTime.UtcNow, "* * * * *");

            await createRecurringJobStateHandler.Handle(new TestMessageContext<CreateRecurringJobState>(message));

            Assert.Single(jobStateRepository.CreatedRecurringJobStates);
            Assert.Single(customJobStateRepository.CreatedFor);
            Assert.Equal(jobStateRepository.CreatedRecurringJobStates[0].JobId, customJobStateRepository.CreatedFor[0]);
        }

        [Fact]
        public async Task PassesMessageThroughToRepository()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var customJobStateRepository = new FakeCustomJobStateRepository();
            var createRecurringJobStateHandler = new CreateRecurringJobStateHandler(jobStateRepository, customJobStateRepository);
            var message = new CreateRecurringJobState("JobType", "Description", DateTime.UtcNow, "* * * * *");

            await createRecurringJobStateHandler.Handle(new TestMessageContext<CreateRecurringJobState>(message));

            Assert.Same(message, jobStateRepository.CreatedRecurringJobStates[0].Message);
        }
    }

    public class Configure
    {
        [Fact]
        public void SetsMaxConcurrentCallsToProcessorCount()
        {
            var createRecurringJobStateHandler = new CreateRecurringJobStateHandler(
                new FakeJobStateRepository(),
                new FakeCustomJobStateRepository());
            var options = new HandlerQueueOptions();

            createRecurringJobStateHandler.Configure(options);

            Assert.Equal(Environment.ProcessorCount, options.MaxConcurrentCalls);
        }
    }
}