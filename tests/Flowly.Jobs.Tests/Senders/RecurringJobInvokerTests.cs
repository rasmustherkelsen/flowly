using Flowly.Jobs.Model;
using Flowly.Jobs.Senders;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.Senders;

public class RecurringJobInvokerTests
{
    public class Submit
    {
        [Fact]
        public async Task SendsEmptyMessageToRecurringJobsQueue()
        {
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var recurringJobInvoker = new RecurringJobInvoker(fakeMessageBusClientRegistry);
            var recurringJob = new RecurringJob(Guid.NewGuid(), "JobTypeName", "desc", "* * * * *", DateTimeOffset.UtcNow, null, null);

            await recurringJobInvoker.Submit(recurringJob);

            Assert.Contains(JobQueuesNames.RecurringJobs, fakeMessageBusClient.CreatedSenders);
            var sender = fakeMessageBusClient.GetSender(JobQueuesNames.RecurringJobs);
            Assert.Single(sender.SentEmptyMessages);
        }

        [Fact]
        public async Task SentMessageContainsJobIdAsMessageId()
        {
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var recurringJobInvoker = new RecurringJobInvoker(fakeMessageBusClientRegistry);
            var jobId = Guid.NewGuid();
            var recurringJob = new RecurringJob(jobId, "JobTypeName", "desc", "* * * * *", DateTimeOffset.UtcNow, null, null);

            await recurringJobInvoker.Submit(recurringJob);

            var sender = fakeMessageBusClient.GetSender(JobQueuesNames.RecurringJobs);
            Assert.Equal(jobId.ToString(), sender.SentEmptyMessages[0].MessageId);
        }

        [Fact]
        public async Task SentMessageContainsJobTypeNameAsSessionId()
        {
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var recurringJobInvoker = new RecurringJobInvoker(fakeMessageBusClientRegistry);
            var recurringJob = new RecurringJob(Guid.NewGuid(), "PayrollJob", "desc", "* * * * *", DateTimeOffset.UtcNow, null, null);

            await recurringJobInvoker.Submit(recurringJob);

            var sender = fakeMessageBusClient.GetSender(JobQueuesNames.RecurringJobs);
            Assert.Equal("PayrollJob", sender.SentEmptyMessages[0].SessionId);
        }
    }
}
