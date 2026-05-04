using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.Tests;

public class MessageHandlerRegistrationExtensionsTests
{
    private static (IFlowlyBuilder FlowlyBuilder, ProviderQueueManifest Manifest) CreateBuilder(string providerName)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(providerName, new StubMessageBusClient(), null);
        services.AddSingleton<IMessageBusClientRegistry>(registry);

        var manifest = new ProviderQueueManifest(providerName, true, "Stub");
        services.AddSingleton(manifest);
        services.AddSingleton<ITopologyNameResolver, KebabCaseTopologyNameResolver>();
        services.AddSingleton<IHandlerSettingsFactory, HandlerSettingsFactory>();
        services.AddSingleton<IQueueRegistrar, QueueRegistrar>();

        return (new StubFlowlyBuilder(services), manifest);
    }

    public class AddMessageHandler
    {
        [Fact]
        public void RegistersHandlerAsScoped()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageHandler<OrderPlaced, OrderPlacedHandler>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(MessageHandler<OrderPlaced>));
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
                .Where(s => s.ServiceType == typeof(IHandlerSettings<OrderPlaced>))
                .Select(s => s.ImplementationInstance)
                .OfType<IHandlerSettings<OrderPlaced>>()
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
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(MessageProcessingBackgroundService<OrderPlaced>));

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
                .Where(s => s.ServiceType == typeof(IHandlerSettings<OrderPlaced>))
                .Select(s => s.ImplementationInstance)
                .OfType<IHandlerSettings<OrderPlaced>>()
                .Single();

            Assert.Equal(3, settings.MaxRetries);
            Assert.Equal(5, settings.RetryDelaySeconds);
        }

        [Fact]
        public void ReturnsBuilderWithQueueAndProviderName()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            var messageHandlerBuilder = flowlyBuilder.AddMessageHandler<OrderPlaced, OrderPlacedHandler>();

            Assert.Equal("order-placed", messageHandlerBuilder.HandlerSettings.QueueName);
            Assert.Equal("primary", messageHandlerBuilder.HandlerSettings.ProviderName);
        }
    }

    public class AddBatchMessageHandler
    {
        [Fact]
        public void RegistersHandlerAsTransient()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddBatchMessageHandler<OrderPlaced, OrderPlacedBatchHandler>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(OrderPlacedBatchHandler));
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersHandlerAgainstAbstractBaseType()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddBatchMessageHandler<OrderPlaced, OrderPlacedBatchHandler>();

            var descriptor = flowlyBuilder.Services.Single(s =>
                s.ServiceType == typeof(BatchMessageHandler<OrderPlaced>));

            Assert.Equal(typeof(OrderPlacedBatchHandler), descriptor.ImplementationType);
        }

        [Fact]
        public void RegistersBackgroundService()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddBatchMessageHandler<OrderPlaced, OrderPlacedBatchHandler>();

            var descriptor = flowlyBuilder.Services.FirstOrDefault(s =>
                s.ImplementationType?.IsGenericType == true &&
                s.ImplementationType.GetGenericTypeDefinition() == typeof(BatchProcessingBackgroundService<>));

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
            throw new NotSupportedException();
        }

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            throw new NotSupportedException();
        }

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        {
            throw new NotSupportedException();
        }

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        {
            throw new NotSupportedException();
        }

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private record OrderPlaced;

    [QueueName("custom-queue")]
    private record NamedMessage;

    private class OrderPlacedHandler : MessageHandler<OrderPlaced>
    {
        public override Task Handle(IMessageContext<OrderPlaced> messageContext)
        {
            return Task.CompletedTask;
        }
    }

    private class NamedMessageHandler : MessageHandler<NamedMessage>
    {
        public override Task Handle(IMessageContext<NamedMessage> messageContext)
        {
            return Task.CompletedTask;
        }
    }

    [RetryPolicy(3, 5)]
    private class RetryingHandler : MessageHandler<OrderPlaced>
    {
        public override Task Handle(IMessageContext<OrderPlaced> messageContext)
        {
            return Task.CompletedTask;
        }
    }

    private class OrderPlacedBatchHandler : BatchMessageHandler<OrderPlaced>
    {
        public override Task Handle(IBatchMessageContext<OrderPlaced> messageContext)
        {
            return Task.CompletedTask;
        }
    }
}