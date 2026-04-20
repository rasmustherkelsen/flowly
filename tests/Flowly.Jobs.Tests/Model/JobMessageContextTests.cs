using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.Model;

public class JobMessageContextTests
{
    public class Message
    {
        [Fact]
        public void ReturnsConstructorMessage()
        {
            var jobId = new JobId();
            var fakeMessageSender = new FakeMessageSender();
            var jobMessageContext = new JobMessageContext<SampleJobMessage>(jobId, new SampleJobMessage("abc"), fakeMessageSender, CancellationToken.None);

            var retrievedMessage = jobMessageContext.Message;

            Assert.Equal("abc", retrievedMessage.Payload);
        }
    }

    public class CancellationTokenProperty
    {
        [Fact]
        public void ReturnsConstructorCancellationToken()
        {
            var jobId = new JobId();
            var fakeMessageSender = new FakeMessageSender();
            using var cancellationTokenSource = new CancellationTokenSource();
            var jobMessageContext = new JobMessageContext<SampleJobMessage>(jobId, new SampleJobMessage("x"), fakeMessageSender, cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, jobMessageContext.CancellationToken);
        }
    }

    public class SaveState
    {
        [Fact]
        public async Task SendsUpdateCustomJobStateMessageWithGivenState()
        {
            var jobId = new JobId();
            var fakeMessageSender = new FakeMessageSender();
            var jobMessageContext = new JobMessageContext<SampleJobMessage>(jobId, new SampleJobMessage("x"), fakeMessageSender, CancellationToken.None);
            var customState = new SampleState(Step: 3);

            await jobMessageContext.SaveState(customState);

            var sentMessage = Assert.IsType<UpdateCustomJobState>(Assert.Single(fakeMessageSender.SentMessages));
            Assert.Equal(jobId, sentMessage.JobId);
            Assert.Same(customState, sentMessage.CustomState);
        }
    }

    private record SampleJobMessage(string Payload) : IJobMessage
    {
        public string Description => "sample";

        public string JobTypeName => "SampleJob";
    }

    private record SampleState(int Step);
}
