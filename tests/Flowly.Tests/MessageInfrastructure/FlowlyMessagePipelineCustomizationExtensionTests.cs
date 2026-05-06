using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.MessageHandlingStrategies;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.Tests.MessageInfrastructure;

public class FlowlyMessagePipelineCustomizationExtensionTests
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
        services.AddSingleton<IHandlerInstrumentation, NullHandlerInstrumentation>();

        return (new StubFlowlyBuilder(services), manifest);
    }

    public class AddMessageProcessor_WithStrategy
    {
        [Fact]
        public void RegistersHandlerAsTransient()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService, StandardMessageHandlingStrategy<TestMessage>>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(TestHandler));
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersStrategyAsTransient()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService, StandardMessageHandlingStrategy<TestMessage>>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(IMessageHandlingStrategy<TestMessage>));
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
            Assert.Equal(typeof(StandardMessageHandlingStrategy<TestMessage>), descriptor.ImplementationType);
        }

        [Fact]
        public void RegistersBackgroundService()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService, StandardMessageHandlingStrategy<TestMessage>>();

            Assert.Contains(flowlyBuilder.Services, s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(TestBackgroundService));
        }

        [Fact]
        public void RegistersHandlerSettingsAsSingleton()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService, StandardMessageHandlingStrategy<TestMessage>>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(IHandlerSettings<TestMessage>));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void AddsQueueToProviderManifest()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService, StandardMessageHandlingStrategy<TestMessage>>();

            var queue = Assert.Single(manifest.Queues);
            Assert.Equal("test", queue.QueueName);
        }

        [Fact]
        public void ReturnsSameBuilder()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            var returned = flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService, StandardMessageHandlingStrategy<TestMessage>>();

            Assert.Same(flowlyBuilder, returned);
        }
    }

    public class AddMessageProcessor_WithoutStrategy
    {
        [Fact]
        public void RegistersHandlerAsTransient()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(TestHandler));
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        }

        [Fact]
        public void DoesNotRegisterAnyStrategy()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService>();

            Assert.DoesNotContain(flowlyBuilder.Services, s => s.ServiceType == typeof(IMessageHandlingStrategy<TestMessage>));
        }

        [Fact]
        public void RegistersBackgroundService()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService>();

            Assert.Contains(flowlyBuilder.Services, s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(TestBackgroundService));
        }

        [Fact]
        public void RegistersHandlerSettingsAsSingleton()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(IHandlerSettings<TestMessage>));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void AddsQueueToProviderManifest()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService>();

            var queue = Assert.Single(manifest.Queues);
            Assert.Equal("test", queue.QueueName);
        }

        [Fact]
        public void ReturnsSameBuilder()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            var returned = flowlyBuilder.AddMessageProcessor<TestMessage, TestHandler, TestBackgroundService>();

            Assert.Same(flowlyBuilder, returned);
        }
    }

    private record TestMessage;

    private class TestHandler;

    private sealed class TestBackgroundService : MessageProcessingBackgroundServiceBase<TestMessage>
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
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

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotSupportedException();
        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotSupportedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotSupportedException();
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotSupportedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
