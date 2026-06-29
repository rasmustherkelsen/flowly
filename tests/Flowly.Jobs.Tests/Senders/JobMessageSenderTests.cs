using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Senders;
using Flowly.Jobs.Tests.Fakes;

namespace Flowly.Jobs.Tests.Senders;

public class JobMessageSenderTests
{
    public class QueueJob
    {
        [Fact]
        public async Task ResolvesJobSubmitterByMessageTypeAndReturnsItsResult()
        {
            var serviceProvider = new FakeServiceProvider();
            var expectedJobId = new JobId(Guid.NewGuid());
            serviceProvider.Register<IJobSubmitter<SomeJobMessage>>(new FakeJobSubmitter<SomeJobMessage>(expectedJobId));
            var jobMessageSender = new JobMessageSender(serviceProvider, new FakeMessageSender());

            var result = await jobMessageSender.QueueJob(new SomeJobMessage());

            Assert.Equal(expectedJobId, result);
        }

        [Fact]
        public async Task PassesCancellationTokenThroughToSubmitter()
        {
            var serviceProvider = new FakeServiceProvider();
            var capturingSubmitter = new FakeJobSubmitter<SomeJobMessage>(new JobId());
            serviceProvider.Register<IJobSubmitter<SomeJobMessage>>(capturingSubmitter);
            var jobMessageSender = new JobMessageSender(serviceProvider, new FakeMessageSender());
            using var cancellationTokenSource = new CancellationTokenSource();

            await jobMessageSender.QueueJob(new SomeJobMessage(), cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, capturingSubmitter.ReceivedToken);
        }
    }

    public class StartRecurringJob
    {
        [Fact]
        public async Task SendsStartRecurringJobMessageWithProvidedJobId()
        {
            var fakeMessageSender = new FakeMessageSender();
            var jobMessageSender = new JobMessageSender(new FakeServiceProvider(), fakeMessageSender);
            var jobId = Guid.NewGuid();

            await jobMessageSender.StartRecurringJob(jobId);

            var sentMessage = Assert.IsType<FlowlysysStartRecurringJobMessage>(Assert.Single(fakeMessageSender.SentMessages));
            Assert.Equal(jobId, sentMessage.JobId);
        }
    }

    private class FakeServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<TService>(TService implementation) where TService : notnull
            => _services[typeof(TService)] = implementation;

        public object? GetService(Type serviceType) => _services.GetValueOrDefault(serviceType);
    }

    private class FakeJobSubmitter<TMessage>(JobId returnedJobId) : IJobSubmitter<TMessage> where TMessage : IJobMessage
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task<JobId> SubmitJob(TMessage message, CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return Task.FromResult(returnedJobId);
        }
    }

    private record SomeJobMessage : IJobMessage
    {
        public string Description => "some description";
        public string JobTypeName => "SomeJob";
    }
}
