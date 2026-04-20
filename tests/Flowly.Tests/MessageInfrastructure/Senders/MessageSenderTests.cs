using Flowly.MessageInfrastructure.Senders;

namespace Flowly.Tests.MessageInfrastructure.Senders;

public class MessageSenderTests
{
    public class Send
    {
        [Fact]
        public async Task ResolvesSubmitterByMessageTypeAndCallsSubmit()
        {
            var submitter = new CapturingSubmitter<SomeMessage>();
            var serviceProvider = new FakeServiceProvider();
            serviceProvider.Register<IMessageSubmitter<SomeMessage>>(submitter);
            var messageSender = new MessageSender(serviceProvider);
            var message = new SomeMessage("payload");

            await messageSender.Send(message);

            Assert.Same(message, submitter.Submitted);
        }

        [Fact]
        public async Task PassesCancellationTokenThroughToSubmitter()
        {
            var submitter = new CapturingSubmitter<SomeMessage>();
            var serviceProvider = new FakeServiceProvider();
            serviceProvider.Register<IMessageSubmitter<SomeMessage>>(submitter);
            var messageSender = new MessageSender(serviceProvider);
            using var cancellationTokenSource = new CancellationTokenSource();

            await messageSender.Send(new SomeMessage("x"), cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, submitter.ReceivedToken);
        }

        [Fact]
        public async Task WithoutRegisteredSubmitter_ThrowsInvalidOperationException()
        {
            var messageSender = new MessageSender(new FakeServiceProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() => messageSender.Send(new SomeMessage("x")));
        }

        [Fact]
        public async Task ResolvesSubmitterByConcreteMessageType_NotInterface()
        {
            var otherSubmitter = new CapturingSubmitter<OtherMessage>();
            var submitter = new CapturingSubmitter<SomeMessage>();
            var serviceProvider = new FakeServiceProvider();
            serviceProvider.Register<IMessageSubmitter<SomeMessage>>(submitter);
            serviceProvider.Register<IMessageSubmitter<OtherMessage>>(otherSubmitter);
            var messageSender = new MessageSender(serviceProvider);

            await messageSender.Send(new SomeMessage("x"));

            Assert.NotNull(submitter.Submitted);
            Assert.Null(otherSubmitter.Submitted);
        }

        [Fact]
        public async Task WhenSubmitterThrows_PropagatesException()
        {
            var submitter = new ThrowingSubmitter<SomeMessage>(new InvalidOperationException("boom"));
            var serviceProvider = new FakeServiceProvider();
            serviceProvider.Register<IMessageSubmitter<SomeMessage>>(submitter);
            var messageSender = new MessageSender(serviceProvider);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => messageSender.Send(new SomeMessage("x")));

            Assert.Equal("boom", exception.Message);
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

    private record OtherMessage;

    private class CapturingSubmitter<TMessage> : IMessageSubmitter<TMessage>
    {
        public TMessage? Submitted { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task Submit(TMessage message, CancellationToken cancellationToken = default)
        {
            Submitted = message;
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private class ThrowingSubmitter<TMessage>(Exception exception) : IMessageSubmitter<TMessage>
    {
        public Task Submit(TMessage message, CancellationToken cancellationToken = default) => Task.FromException(exception);
    }
}
