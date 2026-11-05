using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.SendSteps;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

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

    public class WithSendSteps
    {
        [Fact]
        public async Task StepThatThrows_SendsNothingAndExceptionReachesCaller()
        {
            var sender = new FakeMessageBusSender();
            var messageSender = CreateMessageSender(sender, typeof(RejectingStep));

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => messageSender.Send(new SomeMessage("x")));

            Assert.Equal("rejected", exception.Message);
            Assert.Empty(sender.SentMessages);
        }

        [Fact]
        public async Task StepThatSkipsNext_SendsNothingAndDoesNotThrow()
        {
            var sender = new FakeMessageBusSender();
            var messageSender = CreateMessageSender(sender, typeof(SkippingStep));

            await messageSender.Send(new SomeMessage("x"));

            Assert.Empty(sender.SentMessages);
        }

        [Fact]
        public async Task StepPassingThrough_SendsMessage()
        {
            var sender = new FakeMessageBusSender();
            var messageSender = CreateMessageSender(sender, typeof(PassThroughStep));
            var message = new SomeMessage("x");

            await messageSender.Send(message);

            Assert.Same(message, sender.SentMessages.Single().Message);
        }

        private static MessageSender CreateMessageSender(FakeMessageBusSender sender, params Type[] stepTypes)
        {
            var services = new ServiceCollection();

            foreach (var stepType in stepTypes)
            {
                services.AddScoped(stepType);
            }

            var serviceProvider = services.BuildServiceProvider();
            var registry = new MessageBusClientRegistry();
            registry.Register("primary", new FakeQueueAndStreamClient(sender), null);
            var sendStepRunner = new SendStepRunner(
                stepTypes.Select(stepType => new SendStepRegistration(stepType, SendStepRegistration.ResolveFilterType(stepType))),
                serviceProvider.GetRequiredService<IServiceScopeFactory>());
            var fakeServiceProvider = new FakeServiceProvider();
            fakeServiceProvider.Register<IMessageSubmitter<SomeMessage>>(new MessageSubmitter<SomeMessage>(
                registry,
                "some-message",
                "primary",
                new NullSubmitterInstrumentation(),
                sendStepRunner));

            return new MessageSender(fakeServiceProvider);
        }
    }

    private sealed class RejectingStep : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next)
            => throw new UnauthorizedAccessException("rejected");
    }

    private sealed class SkippingStep : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next) => Task.CompletedTask;
    }

    private sealed class PassThroughStep : SendStep;

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

        public Task Submit(TMessage message, CancellationToken cancellationToken = default, string? partitionKey = null)
        {
            Submitted = message;
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private class ThrowingSubmitter<TMessage>(Exception exception) : IMessageSubmitter<TMessage>
    {
        public Task Submit(TMessage message, CancellationToken cancellationToken = default, string? partitionKey = null) => Task.FromException(exception);
    }
}
