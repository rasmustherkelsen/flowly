using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class MessageHandlerRegistrationExtensionsTests
{
    public class AddMessageHandler
    {
        [Fact]
        public void RegistersHandlerAsScoped()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageHandler<OrderPlaced, OrderPlacedHandler>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(OrderPlacedHandler));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersHandlerAgainstAbstractBaseType()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageHandler<OrderPlaced, OrderPlacedHandler>();

            var descriptor = flowlyBuilder.Services.Single(s =>
                s.ServiceType == typeof(MessageHandler<OrderPlaced>));

            Assert.Equal(typeof(OrderPlacedHandler), descriptor.ImplementationType);
        }

        [Fact]
        public void RegistersHandlerSettingsSingleton()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageHandler<OrderPlaced, OrderPlacedHandler>();

            var settings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(HandlerSettings<OrderPlaced>))
                .Select(s => s.ImplementationInstance)
                .OfType<HandlerSettings<OrderPlaced>>()
                .Single();

            Assert.Equal("order-placed", settings.QueueName);
            Assert.Equal("primary", settings.ProviderName);
            Assert.Equal(nameof(OrderPlacedHandler), settings.HandlerName);
            Assert.False(settings.ReadAndDelete);
        }

        [Fact]
        public void RegistersBackgroundService()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageHandler<OrderPlaced, OrderPlacedHandler>();

            var descriptor = flowlyBuilder.Services.FirstOrDefault(s =>
                s.ImplementationType?.IsGenericType == true &&
                s.ImplementationType.GetGenericTypeDefinition() == typeof(ServiceBusMessageHandlerBackgroundService<>));

            Assert.NotNull(descriptor);
        }

        [Fact]
        public void AddsQueueToProviderManifest()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddMessageHandler<OrderPlaced, OrderPlacedHandler>();

            Assert.Single(manifest.Queues);
            Assert.Equal("order-placed", manifest.Queues[0].QueueName);
            Assert.False(manifest.Queues[0].RequiresSession);
        }

        [Fact]
        public void WithQueueNameAttribute_UsesAttributeValue()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddMessageHandler<NamedMessage, NamedMessageHandler>();

            Assert.Equal("custom-queue", manifest.Queues[0].QueueName);
        }

        [Fact]
        public void WithRetryPolicyAttribute_SetsRetrySettingsOnHandler()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageHandler<OrderPlaced, RetryingHandler>();

            var settings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(HandlerSettings<OrderPlaced>))
                .Select(s => s.ImplementationInstance)
                .OfType<HandlerSettings<OrderPlaced>>()
                .Single();

            Assert.Equal(3, settings.MaxRetries);
            Assert.Equal(5, settings.RetryDelaySeconds);
        }

        [Fact]
        public void ReturnsBuilderWithQueueAndProviderName()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            var messageHandlerBuilder = flowlyBuilder.AddMessageHandler<OrderPlaced, OrderPlacedHandler>();

            Assert.Equal("order-placed", messageHandlerBuilder.QueueName);
            Assert.Equal("primary", messageHandlerBuilder.ProviderName);
        }
    }

    public class AddBatchMessageHandler
    {
        [Fact]
        public void RegistersHandlerAsScoped()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddBatchMessageHandler<OrderPlaced, OrderPlacedBatchHandler>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(OrderPlacedBatchHandler));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersHandlerAgainstAbstractBaseType()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddBatchMessageHandler<OrderPlaced, OrderPlacedBatchHandler>();

            var descriptor = flowlyBuilder.Services.Single(s =>
                s.ServiceType == typeof(BatchMessageHandlerBase<OrderPlaced>));

            Assert.Equal(typeof(OrderPlacedBatchHandler), descriptor.ImplementationType);
        }

        [Fact]
        public void RegistersBatchQueueSettingsSingleton()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddBatchMessageHandler<OrderPlaced, OrderPlacedBatchHandler>();

            var settings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(ServiceBusMessageBatchHandlerBackgroundService<OrderPlaced>.BatchQueueSettings))
                .Select(s => s.ImplementationInstance)
                .OfType<ServiceBusMessageBatchHandlerBackgroundService<OrderPlaced>.BatchQueueSettings>()
                .Single();

            Assert.Equal("order-placed", settings.QueueName);
            Assert.Equal("primary", settings.ProviderName);
        }

        [Fact]
        public void RegistersBackgroundService()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddBatchMessageHandler<OrderPlaced, OrderPlacedBatchHandler>();

            var descriptor = flowlyBuilder.Services.FirstOrDefault(s =>
                s.ImplementationType?.IsGenericType == true &&
                s.ImplementationType.GetGenericTypeDefinition() == typeof(ServiceBusMessageBatchHandlerBackgroundService<>));

            Assert.NotNull(descriptor);
        }

        [Fact]
        public void AddsQueueToProviderManifest()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddBatchMessageHandler<OrderPlaced, OrderPlacedBatchHandler>();

            Assert.Single(manifest.Queues);
            Assert.Equal("order-placed", manifest.Queues[0].QueueName);
        }

        [Fact]
        public void ReturnsOriginalBuilder()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            var returnedBuilder = flowlyBuilder.AddBatchMessageHandler<OrderPlaced, OrderPlacedBatchHandler>();

            Assert.Same(flowlyBuilder, returnedBuilder);
        }
    }

    private static (IFlowlyBuilder FlowlyBuilder, ProviderQueueManifest Manifest) CreateBuilder(string providerName)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(providerName, new StubMessageBusClient(), createTopologyOverride: null);
        services.AddSingleton<IMessageBusClientRegistry>(registry);

        var manifest = new ProviderQueueManifest(providerName, isPrimary: true, "Stub");
        services.AddSingleton(manifest);

        return (new StubFlowlyBuilder(services), manifest);
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
    }

    private sealed class StubMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "Stub";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotSupportedException();
        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotSupportedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotSupportedException();
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotSupportedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private record OrderPlaced;

    [QueueName("custom-queue")]
    private record NamedMessage;

    private class OrderPlacedHandler : MessageHandler<OrderPlaced>
    {
        public override Task Handle(IMessageContext<OrderPlaced> messageContext) => Task.CompletedTask;
    }

    private class NamedMessageHandler : MessageHandler<NamedMessage>
    {
        public override Task Handle(IMessageContext<NamedMessage> messageContext) => Task.CompletedTask;
    }

    [RetryPolicy(3, 5)]
    private class RetryingHandler : MessageHandler<OrderPlaced>
    {
        public override Task Handle(IMessageContext<OrderPlaced> messageContext) => Task.CompletedTask;
    }

    private class OrderPlacedBatchHandler : BatchMessageHandlerBase<OrderPlaced>
    {
        public override Task Handle(IBatchMessageContext<OrderPlaced> messageContext) => Task.CompletedTask;
    }
}
