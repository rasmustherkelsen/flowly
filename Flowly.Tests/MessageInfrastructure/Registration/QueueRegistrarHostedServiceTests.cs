using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class QueueRegistrarHostedServiceTests
{
    public class StartAsync
    {
        [Fact]
        public async Task ValidatorIsCalled()
        {
            var validator = new SpyConflictValidator();
            var service = Build(validator: validator);

            await service.StartAsync(CancellationToken.None);

            Assert.True(validator.WasCalled);
        }

        [Fact]
        public async Task ValidatorThrows_ExceptionPropagatesAndTopologyIsNotCreated()
        {
            var topologyCreator = new SpyTopologyCreator();
            var manifest = ManifestWith("asb", "AzureServiceBus", "order-placed");
            var validator = new ThrowingConflictValidator();
            var service = Build(
                manifests: [manifest],
                topologyCreators: [("asb", topologyCreator)],
                validator: validator);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));

            Assert.False(topologyCreator.WasCalled);
        }

        [Fact]
        public async Task TopologyCreationEnabled_CallsCreateTopologyForEachManifest()
        {
            var creatorA = new SpyTopologyCreator();
            var creatorB = new SpyTopologyCreator();
            var manifests = new[]
            {
                ManifestWith("asb", "AzureServiceBus", "order-placed"),
                ManifestWith("rabbit", "RabbitMQ", "invoice-created")
            };
            var service = Build(
                manifests: manifests,
                topologyCreators: [("asb", creatorA), ("rabbit", creatorB)],
                createTopology: true);

            await service.StartAsync(CancellationToken.None);

            Assert.True(creatorA.WasCalled);
            Assert.True(creatorB.WasCalled);
        }

        [Fact]
        public async Task TopologyCreationGloballyDisabled_DoesNotCallCreateTopology()
        {
            var topologyCreator = new SpyTopologyCreator();
            var manifest = ManifestWith("asb", "AzureServiceBus", "order-placed");
            var service = Build(
                manifests: [manifest],
                topologyCreators: [("asb", topologyCreator)],
                createTopology: false);

            await service.StartAsync(CancellationToken.None);

            Assert.False(topologyCreator.WasCalled);
        }

        [Fact]
        public async Task TransportOverrideDisabled_SkipsTopologyEvenWhenGloballyEnabled()
        {
            var topologyCreator = new SpyTopologyCreator();
            var manifest = ManifestWith("asb", "AzureServiceBus", "order-placed");
            var service = Build(
                manifests: [manifest],
                topologyCreators: [("asb", topologyCreator)],
                createTopology: true,
                transportOverrides: [("asb", false)]);

            await service.StartAsync(CancellationToken.None);

            Assert.False(topologyCreator.WasCalled);
        }

        [Fact]
        public async Task TransportOverrideEnabled_CreatesTopologyEvenWhenGloballyDisabled()
        {
            var topologyCreator = new SpyTopologyCreator();
            var manifest = ManifestWith("asb", "AzureServiceBus", "order-placed");
            var service = Build(
                manifests: [manifest],
                topologyCreators: [("asb", topologyCreator)],
                createTopology: false,
                transportOverrides: [("asb", true)]);

            await service.StartAsync(CancellationToken.None);

            Assert.True(topologyCreator.WasCalled);
        }

        [Fact]
        public async Task QueueDescription_DefaultsAppliedWhenSettingsAreNull()
        {
            var topologyCreator = new SpyTopologyCreator();
            var manifest = ManifestWith("asb", "AzureServiceBus", "order-placed");
            var service = Build(
                manifests: [manifest],
                topologyCreators: [("asb", topologyCreator)]);

            await service.StartAsync(CancellationToken.None);

            var description = Assert.Single(topologyCreator.ReceivedDescriptions);
            Assert.Equal("order-placed", description.Name);
            Assert.Equal(TimeSpan.FromDays(1), description.DefaultMessageTimeToLive);
            Assert.Equal(TimeSpan.FromMinutes(5), description.LockDuration);
            Assert.True(description.DeadLetterOnMessageExpiration);
        }

        [Fact]
        public async Task QueueDescription_ExplicitSettingsArePreserved()
        {
            var ttl = TimeSpan.FromDays(7);
            var lockDuration = TimeSpan.FromMinutes(10);
            var topologyCreator = new SpyTopologyCreator();
            var manifest = new ProviderQueueManifest("asb", isPrimary: true, "AzureServiceBus");
            manifest.Add(new DeferredQueueRegistration(
                "order-placed",
                DefaultMessageTimeToLive: ttl,
                DeadLetterOnMessageExpiration: false,
                LockDuration: lockDuration));
            var service = Build(
                manifests: [manifest],
                topologyCreators: [("asb", topologyCreator)]);

            await service.StartAsync(CancellationToken.None);

            var description = Assert.Single(topologyCreator.ReceivedDescriptions);
            Assert.Equal(ttl, description.DefaultMessageTimeToLive);
            Assert.Equal(lockDuration, description.LockDuration);
            Assert.False(description.DeadLetterOnMessageExpiration);
        }

        [Fact]
        public async Task MultipleQueuesInManifest_AllPassedToTopologyCreator()
        {
            var topologyCreator = new SpyTopologyCreator();
            var manifest = ManifestWith("asb", "AzureServiceBus", "order-placed", "invoice-created", "payment-processed");
            var service = Build(
                manifests: [manifest],
                topologyCreators: [("asb", topologyCreator)]);

            await service.StartAsync(CancellationToken.None);

            Assert.Equal(3, topologyCreator.ReceivedDescriptions.Count);
        }

        private static QueueRegistrarHostedService Build(
            IEnumerable<ProviderQueueManifest>? manifests = null,
            IEnumerable<(string ProviderName, SpyTopologyCreator Creator)>? topologyCreators = null,
            ICrossProviderConflictValidator? validator = null,
            bool createTopology = true,
            IEnumerable<(string ProviderName, bool Override)>? transportOverrides = null)
        {
            manifests ??= [];
            topologyCreators ??= [];
            validator ??= new SpyConflictValidator();

            var clientRegistry = new MessageBusClientRegistry();
            var topologyRegistry = new MessagingTopologyCreatorRegistry();

            var overrides = (transportOverrides ?? []).ToDictionary(x => x.ProviderName, x => (bool?)x.Override);

            foreach (var (providerName, creator) in topologyCreators)
            {
                var topologyOverride = overrides.TryGetValue(providerName, out var value) ? value : null;
                clientRegistry.Register(providerName, new StubMessageBusClient(), topologyOverride);
                topologyRegistry.Register(providerName, creator);
            }

            return new QueueRegistrarHostedService(
                manifests,
                clientRegistry,
                topologyRegistry,
                validator,
                new FlowlyOptions { CreateTopology = createTopology },
                new NullLogger<QueueRegistrarHostedService>());
        }

        private static ProviderQueueManifest ManifestWith(string providerName, string transportType, params string[] queueNames)
        {
            var manifest = new ProviderQueueManifest(providerName, isPrimary: true, transportType);

            foreach (var queueName in queueNames)
                manifest.Add(new DeferredQueueRegistration(queueName));

            return manifest;
        }

        private sealed class SpyConflictValidator : ICrossProviderConflictValidator
        {
            public bool WasCalled { get; private set; }

            public void Validate(IEnumerable<ProviderQueueManifest> manifests)
            {
                WasCalled = true;
            }
        }

        private sealed class ThrowingConflictValidator : ICrossProviderConflictValidator
        {
            public void Validate(IEnumerable<ProviderQueueManifest> manifests) =>
                throw new InvalidOperationException("Simulated conflict.");
        }

        private sealed class SpyTopologyCreator : IMessagingTopologyCreator
        {
            public bool WasCalled { get; private set; }
            public List<IQueueDescription> ReceivedDescriptions { get; } = [];

            public Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
            {
                WasCalled = true;
                ReceivedDescriptions.AddRange(queueDescriptions);
                return Task.CompletedTask;
            }
        }

        private sealed class StubMessageBusClient : IMessageBusClient
        {
            public string MessagingSystem => "stub";
            public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();
            public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();
            public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();
            public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();
            public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();
            public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }
    }
}