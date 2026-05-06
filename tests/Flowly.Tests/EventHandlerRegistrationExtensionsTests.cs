using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.BackgroundServices;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests;

public class EventHandlerRegistrationExtensionsTests
{
    private static (IFlowlyBuilder Builder, ProviderQueueManifest Manifest) CreateBuilder(string providerName)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(providerName, new StubMessageBusClient(), null);
        services.AddSingleton<IMessageBusClientRegistry>(registry);

        var manifest = new ProviderQueueManifest(providerName, true, "AzureServiceBus");
        services.AddSingleton(manifest);

        var builder = new StubFlowlyBuilder(services);
        return (builder, manifest);
    }

    public class AddEventHandler
    {
        [Fact]
        public void RegistersHandlerAsScoped()
        {
            var (builder, _) = CreateBuilder("primary");

            builder.AddEventHandler<OrderPlaced, OrderNotificationHandler>();

            var descriptor = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(OrderNotificationHandler));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersBackgroundService()
        {
            var (builder, _) = CreateBuilder("primary");

            builder.AddEventHandler<OrderPlaced, OrderNotificationHandler>();

            var descriptor = builder.Services.FirstOrDefault(s =>
                s.ImplementationType?.IsGenericType == true &&
                s.ImplementationType.GetGenericTypeDefinition() == typeof(EventHandlerBackgroundService<,>));

            Assert.NotNull(descriptor);
        }

        [Fact]
        public void RegistersSettingsWithDerivedNames()
        {
            var (builder, _) = CreateBuilder("primary");

            builder.AddEventHandler<OrderPlaced, OrderNotificationHandler>();

            var settings = builder.Services
                .Where(s => s.ServiceType == typeof(EventHandlerSettings<OrderPlaced, OrderNotificationHandler>))
                .Select(s => s.ImplementationInstance)
                .OfType<EventHandlerSettings<OrderPlaced, OrderNotificationHandler>>()
                .FirstOrDefault();

            Assert.NotNull(settings);
            Assert.Equal("order-placed", settings.TopicName);
            Assert.Equal("order-notification-handler", settings.SubscriptionName);
        }

        [Fact]
        public void AddsEventToProviderManifest()
        {
            var (builder, manifest) = CreateBuilder("primary");

            builder.AddEventHandler<OrderPlaced, OrderNotificationHandler>();

            Assert.Single(manifest.Events);
            Assert.Equal("order-placed", manifest.Events[0].TopicName);
            Assert.Equal("order-notification-handler", manifest.Events[0].SubscriptionName);
        }

        [Fact]
        public void TwoHandlersSameEvent_BothRegisteredWithDistinctSettings()
        {
            var (builder, manifest) = CreateBuilder("primary");

            builder.AddEventHandler<OrderPlaced, OrderNotificationHandler>();
            builder.AddEventHandler<OrderPlaced, AuditLogHandler>();

            Assert.Equal(2, manifest.Events.Count);

            var notificationSettings = builder.Services
                .Where(s => s.ServiceType == typeof(EventHandlerSettings<OrderPlaced, OrderNotificationHandler>))
                .Select(s => (EventHandlerSettings<OrderPlaced, OrderNotificationHandler>)s.ImplementationInstance!)
                .Single();

            var auditSettings = builder.Services
                .Where(s => s.ServiceType == typeof(EventHandlerSettings<OrderPlaced, AuditLogHandler>))
                .Select(s => (EventHandlerSettings<OrderPlaced, AuditLogHandler>)s.ImplementationInstance!)
                .Single();

            Assert.Equal("order-notification-handler", notificationSettings.SubscriptionName);
            Assert.Equal("audit-log-handler", auditSettings.SubscriptionName);
        }

        [Fact]
        public void WithRetryPolicyAttribute_SetsRetrySettingsOnHandler()
        {
            var (builder, _) = CreateBuilder("primary");

            builder.AddEventHandler<OrderPlaced, RetryingOrderHandler>();

            var settings = builder.Services
                .Where(s => s.ServiceType == typeof(EventHandlerSettings<OrderPlaced, RetryingOrderHandler>))
                .Select(s => (EventHandlerSettings<OrderPlaced, RetryingOrderHandler>)s.ImplementationInstance!)
                .Single();

            Assert.Equal(3, settings.MaxRetries);
            Assert.Equal(10, settings.RetryDelaySeconds);
        }

        [Fact]
        public void WithEventNameAttribute_UsesAttributeValue()
        {
            var (builder, manifest) = CreateBuilder("primary");

            builder.AddEventHandler<NamedEvent, NamedEventHandler>();

            Assert.Equal("custom-event", manifest.Events[0].TopicName);
        }

        [Fact]
        public void ReturnsBuilderWithTopicAndSubscriptionName()
        {
            var (builder, _) = CreateBuilder("primary");

            var eventHandlerBuilder = builder.AddEventHandler<OrderPlaced, OrderNotificationHandler>();

            Assert.Equal("order-placed", eventHandlerBuilder.TopicName);
            Assert.Equal("order-notification-handler", eventHandlerBuilder.SubscriptionName);
        }

        [Fact]
        public void ReturnsBuilderWithProviderName()
        {
            var (builder, _) = CreateBuilder("primary");

            var eventHandlerBuilder = builder.AddEventHandler<OrderPlaced, OrderNotificationHandler>();

            Assert.Equal("primary", eventHandlerBuilder.ProviderName);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private sealed class StubMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "Stub";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        {
            throw new NotImplementedException();
        }

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            throw new NotImplementedException();
        }

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        {
            throw new NotImplementedException();
        }

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        {
            throw new NotImplementedException();
        }

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        {
            throw new NotImplementedException();
        }

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private record OrderPlaced;

    [EventName("custom-event")]
    private record NamedEvent;

    private class OrderNotificationHandler : EventHandlerBase<OrderPlaced>
    {
        public override Task Handle(IEventContext<OrderPlaced> eventContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private class AuditLogHandler : EventHandlerBase<OrderPlaced>
    {
        public override Task Handle(IEventContext<OrderPlaced> eventContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    [RetryPolicy(3, 10)]
    private class RetryingOrderHandler : EventHandlerBase<OrderPlaced>
    {
        public override Task Handle(IEventContext<OrderPlaced> eventContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private class NamedEventHandler : EventHandlerBase<NamedEvent>
    {
        public override Task Handle(IEventContext<NamedEvent> eventContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
