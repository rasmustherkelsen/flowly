using Flowly.MessageInfrastructure.Senders;

namespace Flowly.Tests.MessageInfrastructure.Senders;

public class MessageRecorderTests
{
    public class Record
    {
        [Fact]
        public async Task ResolvesSubmitterByMessageTypeAndCallsSubmit()
        {
            var submitter = new CapturingSubmitter<SomeMessage>();
            var serviceProvider = new FakeServiceProvider();
            serviceProvider.Register<IMessageSubmitter<SomeMessage>>(submitter);
            var messageRecorder = new MessageRecorder(serviceProvider);
            var message = new SomeMessage("payload");

            await messageRecorder.Record(message);

            Assert.Same(message, submitter.Submitted);
        }

        [Fact]
        public async Task PassesCancellationTokenThroughToSubmitter()
        {
            var submitter = new CapturingSubmitter<SomeMessage>();
            var serviceProvider = new FakeServiceProvider();
            serviceProvider.Register<IMessageSubmitter<SomeMessage>>(submitter);
            var messageRecorder = new MessageRecorder(serviceProvider);
            using var cancellationTokenSource = new CancellationTokenSource();

            await messageRecorder.Record(new SomeMessage("x"), cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, submitter.ReceivedToken);
        }

        [Fact]
        public async Task PassesPartitionKeyThroughToSubmitter()
        {
            var submitter = new CapturingSubmitter<SomeMessage>();
            var serviceProvider = new FakeServiceProvider();
            serviceProvider.Register<IMessageSubmitter<SomeMessage>>(submitter);
            var messageRecorder = new MessageRecorder(serviceProvider);

            await messageRecorder.Record(new SomeMessage("x"), partitionKey: "customer-42");

            Assert.Equal("customer-42", submitter.ReceivedPartitionKey);
        }

        [Fact]
        public async Task WithoutPartitionKey_PassesNullThroughToSubmitter()
        {
            var submitter = new CapturingSubmitter<SomeMessage>();
            var serviceProvider = new FakeServiceProvider();
            serviceProvider.Register<IMessageSubmitter<SomeMessage>>(submitter);
            var messageRecorder = new MessageRecorder(serviceProvider);

            await messageRecorder.Record(new SomeMessage("x"));

            Assert.Null(submitter.ReceivedPartitionKey);
        }

        [Fact]
        public async Task WithoutRegisteredSubmitter_ThrowsInvalidOperationException()
        {
            var messageRecorder = new MessageRecorder(new FakeServiceProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() => messageRecorder.Record(new SomeMessage("x")));
        }
    }

    private class FakeServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<TService>(TService implementation) where TService : notnull
            => _services[typeof(TService)] = implementation;

        public object? GetService(Type serviceType) => _services.GetValueOrDefault(serviceType);
    }

    private record SomeMessage(string Value);

    private class CapturingSubmitter<TMessage> : IMessageSubmitter<TMessage>
    {
        public TMessage? Submitted { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public string? ReceivedPartitionKey { get; private set; }

        public Task Submit(TMessage message, CancellationToken cancellationToken = default, string? partitionKey = null)
        {
            Submitted = message;
            ReceivedToken = cancellationToken;
            ReceivedPartitionKey = partitionKey;
            return Task.CompletedTask;
        }
    }
}
