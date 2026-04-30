using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.Registration;

public class QueueRegistrationExtensionsTests
{
    private static (IFlowlyBuilder Builder, ProviderQueueManifest PrimaryManifest) CreateBuilder(string primary, string? secondary = null)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(primary, new StubMessageBusClient(), null);

        if (secondary is not null)
            registry.Register(secondary, new StubMessageBusClient(), null);

        services.AddSingleton<IMessageBusClientRegistry>(registry);

        var primaryManifest = new ProviderQueueManifest(primary, true, "Stub");
        services.AddSingleton(primaryManifest);

        if (secondary is not null)
            services.AddSingleton(new ProviderQueueManifest(secondary, false, "Stub"));

        return (new StubFlowlyBuilder(services), primaryManifest);
    }

    public class AddQueueRegistration
    {
        [Fact]
        public void WithDeferredRegistration_AddsQueueToPrimaryProviderManifest()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration("orders"));

            Assert.Single(manifest.Queues);
            Assert.Equal("orders", manifest.Queues[0].QueueName);
        }

        [Fact]
        public void WithWhitespaceQueueName_IsIgnored()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration("   "));

            Assert.Empty(manifest.Queues);
        }

        [Fact]
        public void WithEmptyQueueName_IsIgnored()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration(""));

            Assert.Empty(manifest.Queues);
        }

        [Fact]
        public void WithStringOverload_CreatesRegistrationAndAdds()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddQueueRegistration("orders", true);

            Assert.Single(manifest.Queues);
            Assert.Equal("orders", manifest.Queues[0].QueueName);
            Assert.True(manifest.Queues[0].RequiresSession);
        }

        [Fact]
        public void WithExplicitProviderName_AddsToNamedProviderManifest()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary", "secondary");

            flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration("orders"), "secondary");

            Assert.Empty(manifest.Queues);
        }

        [Fact]
        public void WithUnknownProviderName_Throws()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            Assert.Throws<InvalidOperationException>(() =>
                flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration("orders"), "ghost"));
        }

        [Fact]
        public void ReturnsTheBuilder_ForFluentChaining()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            var returnedBuilder = flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration("orders"));

            Assert.Same(flowlyBuilder, returnedBuilder);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
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
}
