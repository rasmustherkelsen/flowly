using Flowly.DeadLetters.BackgroundServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.DeadLetters.Tests.Registration;

public class DeadLetterTrackingRegistrationExtensionsTests
{
    private static IEventHandlerBuilder<OrderPlaced> BuildEventHandlerBuilder(
        string topicName,
        string subscriptionName,
        string providerName,
        ServiceCollection? services = null)
    {
        services ??= new ServiceCollection();
        return new StubEventHandlerBuilder<OrderPlaced>(services, topicName, subscriptionName, providerName);
    }

    private static IEventHandlerBuilder<OrderPlaced> BuildEventHandlerBuilderWithDeadLetterTracking(
        string topicName,
        string subscriptionName,
        string providerName,
        ServiceCollection? services = null)
    {
        services ??= new ServiceCollection();
        var flowlyBuilder = new StubFlowlyBuilder(services);
        flowlyBuilder.AddDeadLetterTracking(_ => { });
        return new StubEventHandlerBuilder<OrderPlaced>(services, topicName, subscriptionName, providerName);
    }

    public class WithDeadLetterTrackingForEventHandler
    {
        [Fact]
        public void ThrowsInvalidOperation_WhenDeadLetterTrackingNotConfigured()
        {
            var builder = BuildEventHandlerBuilder("order-placed", "notification-handler", "primary");

            Assert.Throws<InvalidOperationException>(() => builder.WithDeadLetterTracking());
        }

        [Fact]
        public void RegistersIngestionSettings()
        {
            var builder = BuildEventHandlerBuilderWithDeadLetterTracking("order-placed", "notification-handler", "primary");

            builder.WithDeadLetterTracking();

            var settings = builder.Services
                .Where(s => s.ServiceType == typeof(EventSubscriptionDeadLetterIngestionSettings))
                .Select(s => s.ImplementationInstance)
                .OfType<EventSubscriptionDeadLetterIngestionSettings>()
                .FirstOrDefault();

            Assert.NotNull(settings);
            Assert.Equal("order-placed", settings.TopicName);
            Assert.Equal("notification-handler", settings.SubscriptionName);
            Assert.Equal("primary", settings.ProviderName);
        }

        [Fact]
        public void RegistersIngestionBackgroundService()
        {
            var builder = BuildEventHandlerBuilderWithDeadLetterTracking("order-placed", "notification-handler", "primary");

            builder.WithDeadLetterTracking();

            var descriptor = builder.Services.FirstOrDefault(s =>
                s.ImplementationType == typeof(EventSubscriptionDeadLetterIngestionBackgroundService));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void CallingTwiceForSameSubscription_DoesNotRegisterDuplicate()
        {
            var builder = BuildEventHandlerBuilderWithDeadLetterTracking("order-placed", "notification-handler", "primary");

            builder.WithDeadLetterTracking();
            builder.WithDeadLetterTracking();

            var settingsCount = builder.Services
                .Count(s => s.ServiceType == typeof(EventSubscriptionDeadLetterIngestionSettings));

            Assert.Equal(1, settingsCount);
        }

        [Fact]
        public void TwoDistinctSubscriptions_BothRegistered()
        {
            var services = new ServiceCollection();
            var builderA = BuildEventHandlerBuilderWithDeadLetterTracking("order-placed", "notification-handler", "primary", services);
            var builderB = BuildEventHandlerBuilderWithDeadLetterTracking("order-placed", "audit-log-handler", "primary", services);

            builderA.WithDeadLetterTracking();
            builderB.WithDeadLetterTracking();

            var settingsCount = services
                .Count(s => s.ServiceType == typeof(EventSubscriptionDeadLetterIngestionSettings));

            Assert.Equal(2, settingsCount);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
    }

    private sealed class StubEventHandlerBuilder<TEvent>(
        IServiceCollection services,
        string topicName,
        string subscriptionName,
        string providerName) : IEventHandlerBuilder<TEvent>
        where TEvent : class
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public string TopicName => topicName;
        public string SubscriptionName => subscriptionName;
        public string ProviderName => providerName;
    }

    private record OrderPlaced;
}