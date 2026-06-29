using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Senders;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.Senders;

public class JobSubmitterTests
{
    public class SubmitJob
    {
        [Fact]
        public async Task ReturnsNewlyGeneratedJobIdWithNonEmptyGuid()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var queueSettings = new JobSubmitter<SomeJobMessage>.QueueSettings("some-job-queue", "primary");
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, queueSettings, fakeMessageSender);

            var jobId = await jobSubmitter.SubmitJob(new SomeJobMessage());

            Assert.NotEqual(Guid.Empty, jobId.InnerId);
        }

        [Fact]
        public async Task SendsCreateJobStateMessageWithJobTypeNameAndDescription()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var queueSettings = new JobSubmitter<SomeJobMessage>.QueueSettings("some-job-queue", "primary");
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, queueSettings, fakeMessageSender);

            await jobSubmitter.SubmitJob(new SomeJobMessage());

            var createJobState = Assert.IsType<FlowlysysCreateJobStateMessage>(Assert.Single(fakeMessageSender.SentMessages));
            Assert.Equal("SomeJob", createJobState.JobTypeName);
            Assert.Equal("some description", createJobState.Description);
        }

        [Fact]
        public async Task SendsMessageToConfiguredQueueOnConfiguredProviderClient()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var queueSettings = new JobSubmitter<SomeJobMessage>.QueueSettings("some-job-queue", "primary");
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, queueSettings, fakeMessageSender);
            var message = new SomeJobMessage();

            await jobSubmitter.SubmitJob(message);

            var sender = fakeMessageBusClient.GetSender("some-job-queue");
            var (sentMessage, _) = Assert.Single(sender.SentMessages);
            Assert.Same(message, sentMessage);
        }

        [Fact]
        public async Task MessagePropertiesContainJobIdAsMessageId()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var queueSettings = new JobSubmitter<SomeJobMessage>.QueueSettings("some-job-queue", "primary");
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, queueSettings, fakeMessageSender);

            var jobId = await jobSubmitter.SubmitJob(new SomeJobMessage());

            var sender = fakeMessageBusClient.GetSender("some-job-queue");
            var (_, properties) = sender.SentMessages[0];
            Assert.Equal(jobId.InnerId.ToString(), properties.MessageId);
        }

        [Fact]
        public async Task CreateJobStateCarriesSameJobIdAsSubmittedMessage()
        {
            var fakeMessageSender = new FakeMessageSender();
            var fakeMessageBusClient = new FakeMessageBusClient();
            var fakeMessageBusClientRegistry = new FakeMessageBusClientRegistry(fakeMessageBusClient);
            var queueSettings = new JobSubmitter<SomeJobMessage>.QueueSettings("some-job-queue", "primary");
            var jobSubmitter = new JobSubmitter<SomeJobMessage>(fakeMessageBusClientRegistry, queueSettings, fakeMessageSender);

            var jobId = await jobSubmitter.SubmitJob(new SomeJobMessage());

            var createJobState = (FlowlysysCreateJobStateMessage)fakeMessageSender.SentMessages[0];
            Assert.Equal(jobId, createJobState.JobId);
        }
    }

    private record SomeJobMessage : IJobMessage
    {
        public string Description => "some description";
        public string JobTypeName => "SomeJob";
    }
}
