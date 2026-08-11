using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.InMemory.Tests;

public class InMemoryRegistrationTests
{
    private static (IFlowlyBuilder builder, FakeMessageBusClientRegistry registry) CreateBuilder()
    {
        var services = new ServiceCollection();
        var registry = new FakeMessageBusClientRegistry();
        var topologyRegistry = new FakeMessagingTopologyCreatorRegistry();
        var eventTopologyRegistry = new FakeEventTopologyCreatorRegistry();

        services.AddSingleton<IMessageBusClientRegistry>(registry);
        services.AddSingleton<IMessagingTopologyCreatorRegistry>(topologyRegistry);
        services.AddSingleton<IEventTopologyCreatorRegistry>(eventTopologyRegistry);

        var builder = new FakeFlowlyBuilder(services, new ConfigurationBuilder().Build());

        return (builder, registry);
    }

    public class UseInMemory
    {
        [Fact]
        public void RegistersProviderAsPrimary()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseInMemory();

            Assert.Single(registry.GetAll());
            Assert.True(registry.GetAll()[0].IsPrimary);
        }

        [Fact]
        public void WithoutName_RegistersWithDefaultProviderName()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseInMemory();

            Assert.Equal("in-memory", registry.GetAll()[0].Name);
        }

        [Fact]
        public void WithExplicitName_RegistersWithThatName()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseInMemory(name: "secondary");

            Assert.Equal("secondary", registry.GetAll()[0].Name);
        }

        [Fact]
        public void RegistersHostedScheduler()
        {
            var (builder, _) = CreateBuilder();

            builder.UseInMemory();

            var hostedServiceDescriptors = builder.Services
                .Where(s => s.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                .ToList();

            Assert.Single(hostedServiceDescriptors);
        }
    }

    public class UseInMemoryMultipleProviders
    {
        [Fact]
        public void SecondCallWithoutName_ThrowsInvalidOperationException()
        {
            var (builder, _) = CreateBuilder();
            builder.UseInMemory();

            Assert.Throws<InvalidOperationException>(() => builder.UseInMemory());
        }

        [Fact]
        public void SecondCallWithName_RegistersBothProviders()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseInMemory();
            builder.UseInMemory(name: "secondary");

            Assert.Equal(2, registry.GetAll().Count);
        }
    }

    public class RegistrationOrderIndependenceForStreamManifest
    {
        private static (IFlowlyBuilder builder, MessageBusClientRegistry registry) CreateRealBuilder()
        {
            var services = new ServiceCollection();
            var registry = new MessageBusClientRegistry();

            services.AddSingleton<IMessageBusClientRegistry>(registry);
            services.AddSingleton<IMessagingTopologyCreatorRegistry>(new FakeMessagingTopologyCreatorRegistry());
            services.AddSingleton<IEventTopologyCreatorRegistry>(new FakeEventTopologyCreatorRegistry());

            var builder = new FakeFlowlyBuilder(services, new ConfigurationBuilder().Build());
            return (builder, registry);
        }

        [Fact]
        public async Task ManifestPopulatedByRecorderAfterUseInMemory_StillReachesTheBrokerCreatedByUseInMemory()
        {
            var (builder, registry) = CreateRealBuilder();

            // UseInMemory() constructs InMemoryBroker (and captures a StreamQueueManifest reference) before any
            // stream queue has been marked — AddMessageRecorder<T>() only marks "telemetry-reading" as a stream
            // afterward. This proves the broker observes that later mutation rather than a stale/empty snapshot.
            builder.UseInMemory();
            builder.AddMessageRecorder<TelemetryReading>();

            var client = (InMemoryMessageBusClient)registry.GetClient("in-memory");

            var sender = await client.CreateMessageBusSender("telemetry-reading");
            await sender.SendMessage(new TelemetryReading(), MessageProperties.Empty);

            var processor = await client.CreateStreamProcessor<TelemetryReading>(
                "telemetry-reading",
                StartPosition.First(),
                new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock));

            TelemetryReading? received = null;
            processor.ProcessMessage += (msg, _) =>
            {
                received = msg.Body;
                return Task.CompletedTask;
            };

            await processor.StartProcessingMessages();
            await WaitUntil(() => received is not null);
            await processor.StopProcessing(CancellationToken.None);

            Assert.NotNull(received);
        }

        private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.True(condition(), "Condition was not met within timeout.");
        }

        private record TelemetryReading;
    }

    private sealed class FakeFlowlyBuilder(IServiceCollection services, IConfiguration configuration) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => configuration;
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private sealed class FakeMessageBusClientRegistry : IMessageBusClientRegistry
    {
        private readonly List<RegisteredTransport> _transports = [];

        public string PrimaryProviderName => _transports.First(t => t.IsPrimary).Name;

        public void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride)
        {
            var isPrimary = _transports.Count == 0;
            _transports.Add(new RegisteredTransport(providerName, isPrimary, createTopologyOverride));
        }

        public IMessageBusClient GetClient(string providerName) => throw new NotImplementedException();

        public bool IsRegistered(string providerName)
            => _transports.Any(t => string.Equals(t.Name, providerName, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<RegisteredTransport> GetAll() => _transports;
    }

    private sealed class FakeMessagingTopologyCreatorRegistry : IMessagingTopologyCreatorRegistry
    {
        public void Register(string providerName, IMessagingTopologyCreator creator) { }

        public IMessagingTopologyCreator GetCreator(string providerName) => throw new NotImplementedException();
    }

    private sealed class FakeEventTopologyCreatorRegistry : IEventTopologyCreatorRegistry
    {
        public void Register(string providerName, IEventTopologyCreator creator) { }

        public IEventTopologyCreator? TryGetCreator(string providerName) => null;
    }
}
