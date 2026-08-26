namespace Flowly.AzureServiceBus.Tests;

public class EventHandlerAdapterRegistryTests
{
    public class Add
    {
        [Fact]
        public void ReturnsAdapterThatInvokesTheWrappedHandler()
        {
            var invocationCount = 0;
            var eventHandlerAdapterRegistry = new EventHandlerAdapterRegistry<Func<int, Task>, string>(
                handler => args => handler(int.Parse(args)));

            var adapter = eventHandlerAdapterRegistry.Add(_ =>
            {
                invocationCount++;
                return Task.CompletedTask;
            });
            adapter("42");

            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void SameHandlerInstanceSubscribedTwice_DoesNotThrow()
        {
            var eventHandlerAdapterRegistry = new EventHandlerAdapterRegistry<Func<int, Task>, string>(
                handler => args => handler(int.Parse(args)));
            Func<int, Task> handler = _ => Task.CompletedTask;

            eventHandlerAdapterRegistry.Add(handler);
            var exception = Record.Exception(() => eventHandlerAdapterRegistry.Add(handler));

            Assert.Null(exception);
        }

        [Fact]
        public void SameHandlerInstanceSubscribedTwice_ReturnsIndependentAdaptersBothInvokingTheHandler()
        {
            var invocationCount = 0;
            var eventHandlerAdapterRegistry = new EventHandlerAdapterRegistry<Func<int, Task>, string>(
                handler => args => handler(int.Parse(args)));
            Func<int, Task> handler = _ =>
            {
                invocationCount++;
                return Task.CompletedTask;
            };

            var firstAdapter = eventHandlerAdapterRegistry.Add(handler);
            var secondAdapter = eventHandlerAdapterRegistry.Add(handler);
            firstAdapter("1");
            secondAdapter("2");

            Assert.Equal(2, invocationCount);
        }
    }

    public class Remove
    {
        [Fact]
        public void HandlerNeverAdded_ReturnsNull()
        {
            var eventHandlerAdapterRegistry = new EventHandlerAdapterRegistry<Func<int, Task>, string>(
                handler => args => handler(int.Parse(args)));

            var adapter = eventHandlerAdapterRegistry.Remove(_ => Task.CompletedTask);

            Assert.Null(adapter);
        }

        [Fact]
        public void SingleSubscription_ReturnsItsAdapterThenNullOnSecondCall()
        {
            var eventHandlerAdapterRegistry = new EventHandlerAdapterRegistry<Func<int, Task>, string>(
                handler => args => handler(int.Parse(args)));
            Func<int, Task> handler = _ => Task.CompletedTask;
            eventHandlerAdapterRegistry.Add(handler);

            var firstRemoval = eventHandlerAdapterRegistry.Remove(handler);
            var secondRemoval = eventHandlerAdapterRegistry.Remove(handler);

            Assert.NotNull(firstRemoval);
            Assert.Null(secondRemoval);
        }

        [Fact]
        public void SameHandlerSubscribedTwice_RemovesExactlyOneSubscriptionPerCall()
        {
            var eventHandlerAdapterRegistry = new EventHandlerAdapterRegistry<Func<int, Task>, string>(
                handler => args => handler(int.Parse(args)));
            Func<int, Task> handler = _ => Task.CompletedTask;
            eventHandlerAdapterRegistry.Add(handler);
            eventHandlerAdapterRegistry.Add(handler);

            var firstRemoval = eventHandlerAdapterRegistry.Remove(handler);
            var secondRemoval = eventHandlerAdapterRegistry.Remove(handler);
            var thirdRemoval = eventHandlerAdapterRegistry.Remove(handler);

            Assert.NotNull(firstRemoval);
            Assert.NotNull(secondRemoval);
            Assert.Null(thirdRemoval);
        }

        [Fact]
        public void DifferentHandlerInstancesAreTrackedIndependently()
        {
            var eventHandlerAdapterRegistry = new EventHandlerAdapterRegistry<Func<int, Task>, string>(
                handler => args => handler(int.Parse(args)));
            Func<int, Task> firstHandler = _ => Task.CompletedTask;
            Func<int, Task> secondHandler = _ => Task.CompletedTask;
            eventHandlerAdapterRegistry.Add(firstHandler);

            var removalOfUnsubscribedHandler = eventHandlerAdapterRegistry.Remove(secondHandler);
            var removalOfSubscribedHandler = eventHandlerAdapterRegistry.Remove(firstHandler);

            Assert.Null(removalOfUnsubscribedHandler);
            Assert.NotNull(removalOfSubscribedHandler);
        }
    }
}
