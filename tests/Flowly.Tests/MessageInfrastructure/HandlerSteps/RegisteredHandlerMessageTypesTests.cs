using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.HandlerSteps;
using Flowly.MessageInfrastructure.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.HandlerSteps;

public class RegisteredHandlerMessageTypesTests
{
    public class GetAll
    {
        [Fact]
        public void WithNoHandlers_ReturnsEmpty()
        {
            var registeredHandlerMessageTypes = new RegisteredHandlerMessageTypes(new ServiceCollection());

            Assert.Empty(registeredHandlerMessageTypes.GetAll());
        }

        [Fact]
        public void ReturnsMessageTypeOfMessageHandlerSettings()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IHandlerSettings<OrderMessage>>(new HandlerSettings<OrderMessage>("order", "provider", "Handler", false, 1, 0, 0, 0, TimeSpan.Zero));
            var registeredHandlerMessageTypes = new RegisteredHandlerMessageTypes(services);

            Assert.Equal([typeof(OrderMessage)], registeredHandlerMessageTypes.GetAll());
        }

        [Fact]
        public void ReturnsEventTypeOfEventHandlerSettings()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new EventHandlerSettings<OrderPlacedEvent, object>("topic", "subscription", "provider", "Handler"));
            var registeredHandlerMessageTypes = new RegisteredHandlerMessageTypes(services);

            Assert.Equal([typeof(OrderPlacedEvent)], registeredHandlerMessageTypes.GetAll());
        }

        [Fact]
        public void ReturnsEachMessageTypeOnce()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IHandlerSettings<OrderMessage>>(new HandlerSettings<OrderMessage>("order", "provider", "Handler", false, 1, 0, 0, 0, TimeSpan.Zero));
            services.AddSingleton(new EventHandlerSettings<OrderMessage, object>("topic", "subscription", "provider", "Handler"));
            var registeredHandlerMessageTypes = new RegisteredHandlerMessageTypes(services);

            Assert.Equal([typeof(OrderMessage)], registeredHandlerMessageTypes.GetAll());
        }

        [Fact]
        public void IgnoresUnrelatedGenericRegistrations()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEnumerable<string>>([]);
            var registeredHandlerMessageTypes = new RegisteredHandlerMessageTypes(services);

            Assert.Empty(registeredHandlerMessageTypes.GetAll());
        }

        [Fact]
        public void ReflectsHandlersRegisteredAfterConstruction()
        {
            var services = new ServiceCollection();
            var registeredHandlerMessageTypes = new RegisteredHandlerMessageTypes(services);

            services.AddSingleton<IHandlerSettings<OrderMessage>>(new HandlerSettings<OrderMessage>("order", "provider", "Handler", false, 1, 0, 0, 0, TimeSpan.Zero));

            Assert.Equal([typeof(OrderMessage)], registeredHandlerMessageTypes.GetAll());
        }
    }

    private record OrderMessage;

    private record OrderPlacedEvent;
}
