using Flowly.Jobs.MessageHandlers;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Tests.Fakes;
using Flowly.MessageInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Jobs.Tests.MessageHandlers;

public class StartRecurringJobMessageHandlerTests
{
    public class Handle
    {
        [Fact]
        public async Task WithKnownJobId_SendsEmptyMessageToRecurringJobsQueue()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var knownJobId = Guid.NewGuid();
            jobStateRepository.RecurringJobsToReturn =
            [
                new RecurringJob(knownJobId, "SomeJob", "desc", "* * * * *", DateTimeOffset.UtcNow, null, null)
            ];
            var startRecurringJobMessageHandler = new StartRecurringJobMessageHandler(
                jobStateRepository,
                fakeMessageBusClientRegistry,
                new KebabCaseTopologyNameResolver(),
                NullLogger<StartRecurringJobMessageHandler>.Instance);

            await startRecurringJobMessageHandler.Handle(new TestMessageContext<FlowlysysStartRecurringJobMessage>(new FlowlysysStartRecurringJobMessage(knownJobId)));

            Assert.Contains("flowlysys-recurring-jobs", fakeMessageBusClient.CreatedSenders);
            var sender = fakeMessageBusClient.GetSender("flowlysys-recurring-jobs");
            Assert.Single(sender.SentEmptyMessages);
        }

        [Fact]
        public async Task SentMessageHasMessageIdMatchingJobIdAndCorrelationIdMatchingJobTypeName()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var knownJobId = Guid.NewGuid();
            jobStateRepository.RecurringJobsToReturn =
            [
                new RecurringJob(knownJobId, "SomeJobTypeName", "desc", "* * * * *", DateTimeOffset.UtcNow, null, null)
            ];
            var startRecurringJobMessageHandler = new StartRecurringJobMessageHandler(
                jobStateRepository,
                fakeMessageBusClientRegistry,
                new KebabCaseTopologyNameResolver(),
                NullLogger<StartRecurringJobMessageHandler>.Instance);

            await startRecurringJobMessageHandler.Handle(new TestMessageContext<FlowlysysStartRecurringJobMessage>(new FlowlysysStartRecurringJobMessage(knownJobId)));

            var sender = fakeMessageBusClient.GetSender("flowlysys-recurring-jobs");
            Assert.Equal(knownJobId.ToString(), sender.SentEmptyMessages[0].MessageId);
            Assert.Equal("SomeJobTypeName", sender.SentEmptyMessages[0].CorrelationId);
        }

        [Fact]
        public async Task WithUnknownJobId_DoesNotSendAnyMessage()
        {
            var jobStateRepository = new FakeJobStateRepository();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            jobStateRepository.RecurringJobsToReturn = [];
            var startRecurringJobMessageHandler = new StartRecurringJobMessageHandler(
                jobStateRepository,
                fakeMessageBusClientRegistry,
                new KebabCaseTopologyNameResolver(),
                NullLogger<StartRecurringJobMessageHandler>.Instance);

            await startRecurringJobMessageHandler.Handle(new TestMessageContext<FlowlysysStartRecurringJobMessage>(new FlowlysysStartRecurringJobMessage(Guid.NewGuid())));

            Assert.Empty(fakeMessageBusClient.CreatedSenders);
        }
    }
}
