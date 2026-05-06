using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tests.MessageInfrastructure.Model;

public class HandlerRegistrationTests
{
    public class Constructor
    {
        [Fact]
        public void StoresHandlerSettings()
        {
            var handlerSettings = new HandlerSettings<TestMessage>("q", "p", "h", false, 1, 0, 0, 100, TimeSpan.Zero);
            var queueRegistration = new DeferredQueueRegistration("q");

            var handlerRegistration = new HandlerRegistration<TestMessage>(handlerSettings, queueRegistration);

            Assert.Same(handlerSettings, handlerRegistration.HandlerSettings);
        }

        [Fact]
        public void StoresQueueRegistration()
        {
            var handlerSettings = new HandlerSettings<TestMessage>("q", "p", "h", false, 1, 0, 0, 100, TimeSpan.Zero);
            var queueRegistration = new DeferredQueueRegistration("q", true, TimeSpan.FromMinutes(1));

            var handlerRegistration = new HandlerRegistration<TestMessage>(handlerSettings, queueRegistration);

            Assert.Same(queueRegistration, handlerRegistration.QueueRegistration);
        }
    }

    private record TestMessage;
}
