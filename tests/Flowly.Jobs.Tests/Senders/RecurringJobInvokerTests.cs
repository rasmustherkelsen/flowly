using Flowly.Jobs.Model;
using Flowly.Jobs.Senders;
using Flowly.Jobs.Tests.Fakes;
using Flowly.MessageInfrastructure;

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
            var recurringJobInvoker = new RecurringJobInvoker(fakeMessageBusClientRegistry, new KebabCaseTopologyNameResolver());
            var recurringJob = new RecurringJob(Guid.NewGuid(), "JobTypeName", "desc", "* * * * *", DateTimeOffset.UtcNow, null, null);

            await recurringJobInvoker.Submit(recurringJob);

            Assert.Contains("flowlysys-recurring-jobs", fakeMessageBusClient.CreatedSenders);
            var sender = fakeMessageBusClient.GetSender("flowlysys-recurring-jobs");
            Assert.Single(sender.SentEmptyMessages);
        }

        [Fact]
        public async Task SentMessageContainsJobIdAsMessageId()
        {
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var recurringJobInvoker = new RecurringJobInvoker(fakeMessageBusClientRegistry, new KebabCaseTopologyNameResolver());
            var jobId = Guid.NewGuid();
            var recurringJob = new RecurringJob(jobId, "JobTypeName", "desc", "* * * * *", DateTimeOffset.UtcNow, null, null);

            await recurringJobInvoker.Submit(recurringJob);

            var sender = fakeMessageBusClient.GetSender("flowlysys-recurring-jobs");
            Assert.Equal(jobId.ToString(), sender.SentEmptyMessages[0].MessageId);
        }

        [Fact]
        public async Task SentMessageContainsJobTypeNameAsSessionId()
        {
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var recurringJobInvoker = new RecurringJobInvoker(fakeMessageBusClientRegistry, new KebabCaseTopologyNameResolver());
            var recurringJob = new RecurringJob(Guid.NewGuid(), "PayrollJob", "desc", "* * * * *", DateTimeOffset.UtcNow, null, null);

            await recurringJobInvoker.Submit(recurringJob);

            var sender = fakeMessageBusClient.GetSender("flowlysys-recurring-jobs");
            Assert.Equal("PayrollJob", sender.SentEmptyMessages[0].SessionId);
        }
    }
}
