using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Model;
using Flowly.Jobs.Receivers;
using Flowly.Jobs.Registration;
using Flowly.Jobs.Tests.Fakes;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Tests.Registration;

public class JobHandlerRegistrationExtensionsTests
{
    public class AddJobHandler
    {
        [Fact]
        public void RegistersHandlerAsScoped()
        {
            var builder = BuildBuilder();

            builder.AddJobHandler<SomeJobMessage, SomeJobHandler>();

            var descriptor = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(SomeJobHandler));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersJobHandlerBaseMappingAsScoped()
        {
            var builder = BuildBuilder();

            builder.AddJobHandler<SomeJobMessage, SomeJobHandler>();

            var descriptor = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(JobMessageHandlerBase<SomeJobMessage>));
            Assert.NotNull(descriptor);
            Assert.Equal(typeof(SomeJobHandler), descriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersHandlerSettingsWithReadAndDeleteTrue()
        {
            var builder = BuildBuilder();

            builder.AddJobHandler<SomeJobMessage, SomeJobHandler>();

            var settings = builder.Services
                .Where(s => s.ServiceType == typeof(HandlerSettings<SomeJobMessage>))
                .Select(s => (HandlerSettings<SomeJobMessage>)s.ImplementationInstance!)
                .Single();

            Assert.True(settings.ReadAndDelete);
            Assert.Equal(nameof(SomeJobHandler), settings.HandlerName);
        }

        [Fact]
        public void RegistersBackgroundServiceOfCorrectType()
        {
            var builder = BuildBuilder();

            builder.AddJobHandler<SomeJobMessage, SomeJobHandler>();

            var descriptor = builder.Services.FirstOrDefault(s =>
                s.ImplementationType?.IsGenericType == true
                && s.ImplementationType.GetGenericTypeDefinition() == typeof(JobHandlerBackgroundService<>));
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

    private static IFlowlyBuilder BuildBuilder() => BuildBuilderWithManifest().Builder;

    private static (IFlowlyBuilder Builder, ProviderQueueManifest Manifest) BuildBuilderWithManifest(string providerName = "primary")
    {
        var services = new ServiceCollection();
        var registry = new FakeMessageBusClientRegistry(new FakeMessageBusClient(), providerName);
        services.AddSingleton<IMessageBusClientRegistry>(registry);
        var manifest = new ProviderQueueManifest(providerName, isPrimary: true, "Fake");
        services.AddSingleton(manifest);
        var builder = new StubFlowlyBuilder(services);
        return (builder, manifest);
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
    }

    private record SomeJobMessage : IJobMessage
    {
        public string Description => "desc";
        public string JobTypeName => "SomeJob";
    }

    private class SomeJobHandler : JobMessageHandlerBase<SomeJobMessage>
    {
        public override Task Handle(Flowly.Jobs.Model.IJobMessageContext<SomeJobMessage> messageContext) => Task.CompletedTask;
    }
}
