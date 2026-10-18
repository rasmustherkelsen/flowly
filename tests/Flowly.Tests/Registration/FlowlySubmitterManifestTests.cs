using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.Registration;

public class FlowlySubmitterManifestTests
{
    private static IFlowlyBuilder CreateBuilder(string providerName = "primary")
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(providerName, new FakeEventCapableClient(), null);
        services.AddSingleton<IMessageBusClientRegistry>(registry);
        services.AddSingleton(new ProviderQueueManifest(providerName, true, "Stub"));
        return new StubFlowlyBuilder(services);
    }

    private static FlowlySubmitterManifest GetManifest(IFlowlyBuilder builder) =>
        builder.Services
            .Where(s => s.ImplementationInstance is FlowlySubmitterManifest)
            .Select(s => (FlowlySubmitterManifest)s.ImplementationInstance!)
            .Single();

    public class AddMessageSubmitter
    {
        [Fact]
        public void RegistersMessageSubmitter_AddsEntryToManifest()
        {
            var builder = CreateBuilder();

            builder.AddMessageSubmitter<OrderPlaced>();

            var manifest = GetManifest(builder);
            Assert.Single(manifest.Submitters);
            Assert.Equal(typeof(OrderPlaced), manifest.Submitters[0].MessageType);
            Assert.Equal(SubmitterKind.Message, manifest.Submitters[0].Kind);
            Assert.Equal("order-placed", manifest.Submitters[0].QueueOrTopicName);
        }

        [Fact]
        public void RegisteringSameTypeTwice_DoesNotAddDuplicate()
        {
            var builder = CreateBuilder();

            builder.AddMessageSubmitter<OrderPlaced>();
            builder.AddMessageSubmitter<OrderPlaced>();

            var manifest = GetManifest(builder);
            Assert.Single(manifest.Submitters);
        }

        [Fact]
        public void RegisteringDifferentTypes_AddsMultipleEntries()
        {
            var builder = CreateBuilder();

            builder.AddMessageSubmitter<OrderPlaced>();
            builder.AddMessageSubmitter<OrderShipped>();

            var manifest = GetManifest(builder);
            Assert.Equal(2, manifest.Submitters.Count);
        }
    }

    public class AddEventSubmitter
    {
        [Fact]
        public void RegistersEventSubmitter_AddsEventEntryToManifest()
        {
            var builder = CreateBuilder();

            builder.AddEventSubmitter<OrderPlaced>();

            var manifest = GetManifest(builder);
            Assert.Single(manifest.Submitters);
            Assert.Equal(typeof(OrderPlaced), manifest.Submitters[0].MessageType);
            Assert.Equal(SubmitterKind.Event, manifest.Submitters[0].Kind);
        }
    }

    public class GetOrCreate
    {
        [Fact]
        public void WhenNotRegistered_CreatesAndRegistersNewManifest()
        {
            var services = new ServiceCollection();

            var manifest = FlowlySubmitterManifest.GetOrCreate(services);

            Assert.NotNull(manifest);
            Assert.Single(services, s => s.ImplementationInstance is FlowlySubmitterManifest);
        }

        [Fact]
        public void WhenAlreadyRegistered_ReturnsSameInstance()
        {
            var services = new ServiceCollection();
            var first = FlowlySubmitterManifest.GetOrCreate(services);

            var second = FlowlySubmitterManifest.GetOrCreate(services);

            Assert.Same(first, second);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private record OrderPlaced;
    private record OrderShipped;
}
