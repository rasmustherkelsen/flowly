using Flowly.MessageInfrastructure.Events;

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

        public Task Raise(TEvent @event, CancellationToken cancellationToken = default)
        {
            Raised = @event;
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private class ThrowingSubmitter<TEvent>(Exception exception) : IEventSubmitter<TEvent>
    {
        public Task Raise(TEvent @event, CancellationToken cancellationToken = default) => Task.FromException(exception);
    }

    private record OrderPlaced(string OrderId);
}
