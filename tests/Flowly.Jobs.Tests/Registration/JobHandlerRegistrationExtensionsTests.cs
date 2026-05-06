using Flowly.Jobs.Tests.Fakes;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.Jobs.Tests.Registration;

public class JobHandlerRegistrationExtensionsTests
{
    private static IFlowlyBuilder BuildBuilder()
    {
        return BuildBuilderWithManifest().Builder;
    }

    private static (IFlowlyBuilder Builder, ProviderQueueManifest Manifest) BuildBuilderWithManifest(string providerName = "primary")
    {
        var services = new ServiceCollection();
        var registry = new FakeMessageBusClientRegistry(new FakeMessageBusClient(), providerName);
        services.AddSingleton<IMessageBusClientRegistry>(registry);
        var manifest = new ProviderQueueManifest(providerName, true, "Fake");
        services.AddSingleton(manifest);
        services.AddSingleton<ITopologyNameResolver, KebabCaseTopologyNameResolver>();
        services.AddSingleton<IHandlerSettingsFactory, HandlerSettingsFactory>();
        services.AddSingleton<IQueueRegistrar, QueueRegistrar>();
        var builder = new StubFlowlyBuilder(services);
        return (builder, manifest);
    }

    public class AddJobHandler
    {
        [Fact]
        public void RegistersHandlerAsScoped()
        {
            var builder = BuildBuilder();

            builder.AddJobHandler<SomeJobMessage, SomeJobHandler>();

            var descriptor = builder.Services.Single(s => s.ServiceType == typeof(JobHandler<SomeJobMessage>));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersJobHandlerBaseMappingAsScoped()
        {
            var builder = BuildBuilder();

            builder.AddJobHandler<SomeJobMessage, SomeJobHandler>();

            var descriptor = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(JobHandler<SomeJobMessage>));
            Assert.NotNull(descriptor);
            Assert.Equal(typeof(SomeJobHandler), descriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersHandlerSettingsWithReadAndDeleteFalse()
        {
            var builder = BuildBuilder();

            builder.AddJobHandler<SomeJobMessage, SomeJobHandler>();

            var settings = builder.Services
                .Where(s => s.ServiceType == typeof(IHandlerSettings<SomeJobMessage>))
                .Select(s => (IHandlerSettings<SomeJobMessage>)s.ImplementationInstance!)
                .Single();

            Assert.False(settings.ReadAndDelete);
            Assert.Equal(nameof(SomeJobHandler), settings.HandlerName);
        }

        [Fact]
        public void RegistersBackgroundService()
        {
            var builder = BuildBuilder();

            builder.AddJobHandler<SomeJobMessage, SomeJobHandler>();

            var descriptor = builder.Services.FirstOrDefault(s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(MessageProcessingBackgroundService<SomeJobMessage>));
            Assert.NotNull(descriptor);
        }

        [Fact]
        public void AddsQueueRegistrationToPrimaryProviderManifest()
        {
            var (builder, manifest) = BuildBuilderWithManifest();

            builder.AddJobHandler<SomeJobMessage, SomeJobHandler>();

            Assert.Single(manifest.Queues);
            Assert.Equal("some-job", manifest.Queues[0].QueueName);
        }

        [Fact]
        public void ReturnsSameBuilderInstance()
        {
            var builder = BuildBuilder();

            var result = builder.AddJobHandler<SomeJobMessage, SomeJobHandler>();

            Assert.Same(builder, result);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private record SomeJobMessage : IJobMessage
    {
        public string Description => "desc";
        public string JobTypeName => "SomeJob";
    }

    private class SomeJobHandler : JobHandler<SomeJobMessage>
    {
        public override Task Handle(IJobMessageContext<SomeJobMessage> messageContext)
        {
            return Task.CompletedTask;
        }
    }
}