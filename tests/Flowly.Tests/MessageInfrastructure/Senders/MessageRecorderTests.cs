using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.SendSteps;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

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
        public async Task PassesCustomPropertiesThroughToSubmitter()
        {
            var submitter = new CapturingSubmitter<SomeMessage>();
            var serviceProvider = new FakeServiceProvider();
            serviceProvider.Register<IMessageSubmitter<SomeMessage>>(submitter);
            var messageRecorder = new MessageRecorder(serviceProvider);
            var customProperties = new Dictionary<string, string> { ["tenant"] = "contoso" };

            await messageRecorder.Record(new SomeMessage("x"), customProperties: customProperties);

            Assert.Same(customProperties, submitter.ReceivedCustomProperties);
        }

        [Fact]
        public async Task WithoutRegisteredSubmitter_ThrowsInvalidOperationException()
        {
            var messageRecorder = new MessageRecorder(new FakeServiceProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() => messageRecorder.Record(new SomeMessage("x")));
        }
    }

    public class WithSendSteps
    {
        [Fact]
        public async Task StepThatThrows_SendsNothingAndExceptionReachesCaller()
        {
            var sender = new FakeMessageBusSender();
            var messageRecorder = CreateMessageRecorder(sender, typeof(RejectingStep));

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => messageRecorder.Record(new SomeMessage("x")));

            Assert.Equal("rejected", exception.Message);
            Assert.Empty(sender.SentMessages);
        }

        [Fact]
        public async Task StepThatSkipsNext_SendsNothingAndDoesNotThrow()
        {
            var sender = new FakeMessageBusSender();
            var messageRecorder = CreateMessageRecorder(sender, typeof(SkippingStep));

            await messageRecorder.Record(new SomeMessage("x"));

            Assert.Empty(sender.SentMessages);
        }

        [Fact]
        public async Task StepPassingThrough_SendsMessage()
        {
            var sender = new FakeMessageBusSender();
            var messageRecorder = CreateMessageRecorder(sender, typeof(PassThroughStep));
            var message = new SomeMessage("x");

            await messageRecorder.Record(message);

            Assert.Same(message, sender.SentMessages.Single().Message);
        }

        [Fact]
        public async Task PreservesPartitionKeyWhenStepCallsNext()
        {
            var sender = new FakeMessageBusSender();
            var messageRecorder = CreateMessageRecorder(sender, typeof(PassThroughStep));

            await messageRecorder.Record(new SomeMessage("x"), partitionKey: "customer-9");

            Assert.Equal("customer-9", sender.SentMessages.Single().Properties.PartitionKey);
        }

        private static MessageRecorder CreateMessageRecorder(FakeMessageBusSender sender, params Type[] stepTypes)
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
                sendStepRunner,
                isStream: true));

            return new MessageRecorder(fakeServiceProvider);
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

    private class CapturingSubmitter<TMessage> : IMessageSubmitter<TMessage>
    {
        public TMessage? Submitted { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public string? ReceivedPartitionKey { get; private set; }
        public IReadOnlyDictionary<string, string>? ReceivedCustomProperties { get; private set; }

        public Task Submit(TMessage message, CancellationToken cancellationToken = default, string? partitionKey = null, IReadOnlyDictionary<string, string>? customProperties = null)
        {
            ReceivedCustomProperties = customProperties;
            Submitted = message;
            ReceivedToken = cancellationToken;
            ReceivedPartitionKey = partitionKey;
            return Task.CompletedTask;
        }
    }
}
