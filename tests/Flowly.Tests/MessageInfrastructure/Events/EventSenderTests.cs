using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.SendSteps;
using Flowly.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Events;

public class EventSenderTests
{
    public class RaiseEvent
    {
        [Fact]
        public async Task ResolvesSubmitterByEventTypeAndCallsRaise()
        {
            var serviceProvider = new FakeServiceProvider();
            var capturingSubmitter = new CapturingSubmitter<OrderPlaced>();
            serviceProvider.Register<IEventSubmitter<OrderPlaced>>(capturingSubmitter);
            var eventSender = new EventSender(serviceProvider);
            var @event = new OrderPlaced("order-1");

            await eventSender.RaiseEvent(@event);

            Assert.Same(@event, capturingSubmitter.Raised);
        }

        [Fact]
        public async Task PassesCancellationTokenThroughToSubmitter()
        {
            var serviceProvider = new FakeServiceProvider();
            var capturingSubmitter = new CapturingSubmitter<OrderPlaced>();
            serviceProvider.Register<IEventSubmitter<OrderPlaced>>(capturingSubmitter);
            var eventSender = new EventSender(serviceProvider);
            using var cancellationTokenSource = new CancellationTokenSource();

            await eventSender.RaiseEvent(new OrderPlaced("x"), cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, capturingSubmitter.ReceivedToken);
        }

        [Fact]
        public async Task PassesCustomPropertiesThroughToSubmitter()
        {
            var serviceProvider = new FakeServiceProvider();
            var capturingSubmitter = new CapturingSubmitter<OrderPlaced>();
            serviceProvider.Register<IEventSubmitter<OrderPlaced>>(capturingSubmitter);
            var eventSender = new EventSender(serviceProvider);
            var customProperties = new Dictionary<string, string> { ["tenant"] = "contoso" };

            await eventSender.RaiseEvent(new OrderPlaced("x"), customProperties: customProperties);

            Assert.Same(customProperties, capturingSubmitter.ReceivedCustomProperties);
        }

        [Fact]
        public async Task WithoutRegisteredSubmitter_ThrowsInvalidOperationException()
        {
            var eventSender = new EventSender(new FakeServiceProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() => eventSender.RaiseEvent(new OrderPlaced("x")));
        }

        [Fact]
        public async Task WhenSubmitterThrows_PropagatesException()
        {
            var serviceProvider = new FakeServiceProvider();
            serviceProvider.Register<IEventSubmitter<OrderPlaced>>(new ThrowingSubmitter<OrderPlaced>(new InvalidOperationException("boom")));
            var eventSender = new EventSender(serviceProvider);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => eventSender.RaiseEvent(new OrderPlaced("x")));

            Assert.Equal("boom", exception.Message);
        }
    }

    public class WithSendSteps
    {
        [Fact]
        public async Task StepThatThrows_PublishesNothingAndExceptionReachesCaller()
        {
            var publisher = new FakeMessageBusSender();
            var eventSender = CreateEventSender(publisher, typeof(RejectingStep));

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => eventSender.RaiseEvent(new OrderPlaced("x")));

            Assert.Equal("rejected", exception.Message);
            Assert.Empty(publisher.SentMessages);
        }

        [Fact]
        public async Task StepThatSkipsNext_PublishesNothingAndDoesNotThrow()
        {
            var publisher = new FakeMessageBusSender();
            var eventSender = CreateEventSender(publisher, typeof(SkippingStep));

            await eventSender.RaiseEvent(new OrderPlaced("x"));

            Assert.Empty(publisher.SentMessages);
        }

        [Fact]
        public async Task StepPassingThrough_PublishesEvent()
        {
            var publisher = new FakeMessageBusSender();
            var eventSender = CreateEventSender(publisher, typeof(PassThroughStep));
            var @event = new OrderPlaced("x");

            await eventSender.RaiseEvent(@event);

            Assert.Same(@event, publisher.SentMessages.Single().Message);
        }

        private static EventSender CreateEventSender(FakeMessageBusSender publisher, params Type[] stepTypes)
        {
            var services = new ServiceCollection();

            foreach (var stepType in stepTypes)
            {
                services.AddScoped(stepType);
            }

            var serviceProvider = services.BuildServiceProvider();
            var registry = new MessageBusClientRegistry();
            registry.Register("primary", new FakeEventCapableClient(publisher), null);
            var sendStepRunner = new SendStepRunner(
                stepTypes.Select(stepType => new SendStepRegistration(stepType, SendStepRegistration.ResolveFilterType(stepType))),
                serviceProvider.GetRequiredService<IServiceScopeFactory>());
            var fakeServiceProvider = new FakeServiceProvider();
            fakeServiceProvider.Register<IEventSubmitter<OrderPlaced>>(new EventSubmitter<OrderPlaced>(
                registry,
                "order-placed",
                "primary",
                new NullEventPublisherInstrumentation(),
                sendStepRunner));

            return new EventSender(fakeServiceProvider);
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

    private class CapturingSubmitter<TEvent> : IEventSubmitter<TEvent>
    {
        public TEvent? Raised { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public IReadOnlyDictionary<string, string>? ReceivedCustomProperties { get; private set; }

        public Task Raise(TEvent @event, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string>? customProperties = null)
        {
            ReceivedCustomProperties = customProperties;
            Raised = @event;
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private class ThrowingSubmitter<TEvent>(Exception exception) : IEventSubmitter<TEvent>
    {
        public Task Raise(TEvent @event, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string>? customProperties = null) => Task.FromException(exception);
    }

    private record OrderPlaced(string OrderId);
}
