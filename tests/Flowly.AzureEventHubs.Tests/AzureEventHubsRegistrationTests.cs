using Azure.Core;
using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.AzureEventHubs.Tests;

public class AzureEventHubsRegistrationTests
{
    private const string ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=key;SharedAccessKey=c2VjcmV0";

    public class UseAzureEventHubs
    {
        [Fact]
        public void WithConnectionString_RegistersProviderWithDefaultName()
        {
            var builder = TestFlowlyBuilder.Create();

            builder.UseAzureEventHubs(ConnectionString);

            Assert.Equal("azure-event-hubs", Assert.Single(ProviderNameResolver.GetRegistry(builder.Services).GetAll()).Name);
        }

        [Fact]
        public void WithExplicitName_RegistersProviderWithThatName()
        {
            var builder = TestFlowlyBuilder.Create();

            builder.UseAzureEventHubs(ConnectionString, "telemetry");

            Assert.Equal("telemetry", Assert.Single(ProviderNameResolver.GetRegistry(builder.Services).GetAll()).Name);
        }

        [Fact]
        public void WithConnectionStringName_DoesNotRequireTheConnectionStringToBeResolvedAtRegistration()
        {
            var builder = TestFlowlyBuilder.Create(new ConfigurationBuilder().Build());

            builder.UseAzureEventHubs("EventHubs");

            Assert.Single(ProviderNameResolver.GetRegistry(builder.Services).GetAll());
        }

        [Fact]
        public void WithCredential_RegistersProviderWithDefaultName()
        {
            var builder = TestFlowlyBuilder.Create();

            builder.UseAzureEventHubs("example.servicebus.windows.net", new FakeTokenCredential());

            Assert.Equal("azure-event-hubs", Assert.Single(ProviderNameResolver.GetRegistry(builder.Services).GetAll()).Name);
        }

        [Fact]
        public void RegistersStreamAndPartitionedStreamCapabilitiesWithoutQueueCapability()
        {
            var builder = TestFlowlyBuilder.Create();

            builder.UseAzureEventHubs(ConnectionString);

            var client = ProviderNameResolver.GetRegistry(builder.Services).GetTransportClient("azure-event-hubs");
            Assert.IsAssignableFrom<IStreamCapableMessageBusClient>(client);
            Assert.IsAssignableFrom<IPartitionedStreamCapableMessageBusClient>(client);
            Assert.IsNotAssignableFrom<IMessageBusClient>(client);
        }

        [Fact]
        public void ForcesTopologyCreationOffSoTheValidatorAlwaysRuns()
        {
            var builder = TestFlowlyBuilder.Create();

            builder.UseAzureEventHubs(ConnectionString);

            Assert.False(Assert.Single(ProviderNameResolver.GetRegistry(builder.Services).GetAll()).CreateTopologyOverride);
        }

        [Fact]
        public void IsNeverThePrimaryProvider()
        {
            var builder = TestFlowlyBuilder.Create();

            builder.UseAzureEventHubs(ConnectionString);

            Assert.False(Assert.Single(ProviderNameResolver.GetRegistry(builder.Services).GetAll()).IsPrimary);
            Assert.False(Assert.IsType<ProviderQueueManifest>(Assert.Single(builder.Services, service => service.ImplementationInstance is ProviderQueueManifest).ImplementationInstance).IsPrimary);
        }

        [Fact]
        public void RegistersProviderManifestWithTransportType()
        {
            var builder = TestFlowlyBuilder.Create();

            builder.UseAzureEventHubs(ConnectionString);

            var manifest = Assert.IsType<ProviderQueueManifest>(Assert.Single(builder.Services, service => service.ImplementationInstance is ProviderQueueManifest).ImplementationInstance);
            Assert.Equal("AzureEventHubs", manifest.TransportType);
            Assert.Equal("azure-event-hubs", manifest.ProviderName);
        }

        [Fact]
        public void RegistersTopologyValidatorForTheProvider()
        {
            var builder = TestFlowlyBuilder.Create();
            builder.Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            builder.UseAzureEventHubs(ConnectionString, "telemetry");

            using var serviceProvider = builder.Services.BuildServiceProvider();
            Assert.Equal("telemetry", Assert.Single(serviceProvider.GetServices<IMessagingTopologyValidator>()).ProviderName);
        }

        [Fact]
        public void RegistersConnectionLifetimeHostedService()
        {
            var builder = TestFlowlyBuilder.Create();

            builder.UseAzureEventHubs(ConnectionString);

            Assert.Single(builder.Services, service => service.ServiceType == typeof(IHostedService));
        }

        [Fact]
        public void SecondProviderWithoutName_Throws()
        {
            var builder = TestFlowlyBuilder.Create();
            builder.UseAzureEventHubs(ConnectionString);

            var exception = Assert.Throws<InvalidOperationException>(() => builder.UseAzureEventHubs(ConnectionString));

            Assert.Contains("Secondary Azure Event Hubs providers must have an explicit name", exception.Message);
        }

        [Fact]
        public void SecondProviderWithName_RegistersBothProviders()
        {
            var builder = TestFlowlyBuilder.Create();
            builder.UseAzureEventHubs(ConnectionString);

            builder.UseAzureEventHubs(ConnectionString, "secondary");

            Assert.Equal(2, ProviderNameResolver.GetRegistry(builder.Services).GetAll().Count);
        }

        [Fact]
        public void WithQueueProviderRegisteredFirst_QueueProviderStaysPrimary()
        {
            var builder = TestFlowlyBuilder.Create().WithQueueProvider();

            builder.UseAzureEventHubs(ConnectionString, "events");

            var registry = ProviderNameResolver.GetRegistry(builder.Services);
            Assert.Equal("queues", registry.PrimaryProviderName);
        }

        [Fact]
        public void WithEventHubsRegisteredBeforeQueueProvider_QueueProviderBecomesPrimary()
        {
            var builder = TestFlowlyBuilder.Create();
            builder.UseAzureEventHubs(ConnectionString);

            builder.WithQueueProvider();

            Assert.Equal("queues", ProviderNameResolver.GetRegistry(builder.Services).PrimaryProviderName);
        }
        [Fact]
        public void WithLoadBalancingIntervalAndPartitionOwnershipExpiration_RegistersTheProvider()
        {
            var builder = TestFlowlyBuilder.Create();

            builder.UseAzureEventHubs(ConnectionString, loadBalancingInterval: TimeSpan.FromSeconds(10), partitionOwnershipExpiration: TimeSpan.FromSeconds(60));

            Assert.Single(ProviderNameResolver.GetRegistry(builder.Services).GetAll());
        }

        [Fact]
        public void WithCredentialAndIntervals_RegistersTheProvider()
        {
            var builder = TestFlowlyBuilder.Create();

            builder.UseAzureEventHubs("example.servicebus.windows.net", new FakeTokenCredential(), loadBalancingInterval: TimeSpan.FromSeconds(10), partitionOwnershipExpiration: TimeSpan.FromSeconds(60));

            Assert.Single(ProviderNameResolver.GetRegistry(builder.Services).GetAll());
        }

        [Fact]
        public void WithLoadBalancingIntervalOfZero_ThrowsAndRegistersNothing()
        {
            var builder = TestFlowlyBuilder.Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.UseAzureEventHubs(ConnectionString, loadBalancingInterval: TimeSpan.Zero));
            Assert.Empty(ProviderNameResolver.GetRegistry(builder.Services).GetAll());
        }

        [Fact]
        public void WithPartitionOwnershipExpirationOfZero_Throws()
        {
            var builder = TestFlowlyBuilder.Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.UseAzureEventHubs(ConnectionString, partitionOwnershipExpiration: TimeSpan.Zero));
        }

        [Fact]
        public void WithExpirationNotGreaterThanTheInterval_Throws()
        {
            var builder = TestFlowlyBuilder.Create();

            Assert.Throws<ArgumentException>(() => builder.UseAzureEventHubs(ConnectionString, loadBalancingInterval: TimeSpan.FromSeconds(60), partitionOwnershipExpiration: TimeSpan.FromSeconds(30)));
        }

        [Fact]
        public void WithCredentialAndExpirationNotGreaterThanTheInterval_Throws()
        {
            var builder = TestFlowlyBuilder.Create();

            Assert.Throws<ArgumentException>(
                () => builder.UseAzureEventHubs("example.servicebus.windows.net", new FakeTokenCredential(), loadBalancingInterval: TimeSpan.FromMinutes(5)));
        }
    }

    public class Routing
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void StreamContractWithoutAffinity_LandsOnEventHubsInEitherRegistrationOrder(bool queueProviderFirst)
        {
            var builder = TestFlowlyBuilder.Create();

            if (queueProviderFirst)
            {
                builder.WithQueueProvider().WithEventHubs();
            }
            else
            {
                builder.WithEventHubs().WithQueueProvider();
            }

            Assert.Equal("azure-event-hubs", ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(builder.Services, typeof(TelemetryReading)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void QueueContractWithoutAffinity_DoesNotLandOnEventHubsInEitherRegistrationOrder(bool queueProviderFirst)
        {
            var builder = TestFlowlyBuilder.Create();

            if (queueProviderFirst)
            {
                builder.WithQueueProvider().WithEventHubs();
            }
            else
            {
                builder.WithEventHubs().WithQueueProvider();
            }

            Assert.Equal("queues", ProviderNameResolver.Resolve<IMessageBusClient>(builder.Services, typeof(TelemetryReading)));
        }

        [Fact]
        public void AddMessageRecorderWithBothProviders_RegistersTheStreamQueueOnEventHubs()
        {
            var builder = TestFlowlyBuilder.Create().WithQueueProvider().WithEventHubs();

            builder.AddMessageRecorder<TelemetryReading>();

            var manifests = builder.Services.Select(service => service.ImplementationInstance).OfType<ProviderQueueManifest>().ToList();
            Assert.Contains("telemetry-reading", Assert.Single(manifests, manifest => manifest.ProviderName == "azure-event-hubs").Queues.Select(queue => queue.QueueName));
            Assert.Empty(Assert.Single(manifests, manifest => manifest.ProviderName == "queues").Queues);
        }

        [Fact]
        public void AddMessageSubmitterWithOnlyEventHubs_Throws()
        {
            var builder = TestFlowlyBuilder.Create().WithEventHubs();

            var exception = Assert.Throws<InvalidOperationException>(() => builder.AddMessageSubmitter<TelemetryReading>());

            Assert.Contains("TelemetryReading", exception.Message);
            Assert.Contains("azure-event-hubs", exception.Message);
        }

        [Fact]
        public void AddEventSubmitterWithOnlyEventHubs_Throws()
        {
            var builder = TestFlowlyBuilder.Create().WithEventHubs();

            Assert.Throws<InvalidOperationException>(() => builder.AddEventSubmitter<TelemetryReading>());
        }

        [Fact]
        public void QueueContractWithAffinityToEventHubs_Throws()
        {
            var builder = TestFlowlyBuilder.Create().WithQueueProvider().WithEventHubs();

            var exception = Assert.Throws<InvalidOperationException>(() => builder.AddMessageSubmitter<AffineTelemetryReading>());

            Assert.Contains("azure-event-hubs", exception.Message);
        }

        [Fact]
        public void PartitionedStreamContract_IsAcceptedByEventHubs()
        {
            var builder = TestFlowlyBuilder.Create().WithEventHubs();

            builder.AddMessageRecorder<PartitionedTelemetryReading>();

            Assert.Equal(4, builder.Services.Select(service => service.ImplementationInstance).OfType<StreamQueueManifest>().Single().GetPartitionCount("partitioned-telemetry-reading"));
        }
    }

    public class Startup
    {
        [Fact]
        public async Task WithCreateTopologyRequested_StillValidatesTheEventHubs()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var inspector = new FakeEventHubsInspector(["0"]);
            clientFactory.Inspectors["telemetry-reading"] = inspector;
            var builder = TestFlowlyBuilder.Create().WithEventHubs(clientFactory);
            builder.AddMessageRecorder<TelemetryReading>();

            await StartQueueRegistrar(builder, createTopology: true);

            Assert.Equal(1, inspector.Calls);
        }

        [Fact]
        public async Task WithCreateTopologyRequestedAndDriftedPartitions_FailsHostStartup()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            clientFactory.Inspectors["partitioned-telemetry-reading"] = new FakeEventHubsInspector(["0", "1"]);
            var builder = TestFlowlyBuilder.Create().WithEventHubs(clientFactory);
            builder.AddMessageRecorder<PartitionedTelemetryReading>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => StartQueueRegistrar(builder, createTopology: true));
        }

        [Fact]
        public async Task WithCreateTopologyDisabled_ValidatesTheEventHubs()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var inspector = new FakeEventHubsInspector(["0"]);
            clientFactory.Inspectors["telemetry-reading"] = inspector;
            var builder = TestFlowlyBuilder.Create().WithEventHubs(clientFactory);
            builder.AddMessageRecorder<TelemetryReading>();

            await StartQueueRegistrar(builder, createTopology: false);

            Assert.Equal(1, inspector.Calls);
        }

        [Fact]
        public async Task WithAGeoReplicatedEventHubAndARegisteredCheckpointStore_WarnsAtStartup()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            clientFactory.Inspectors["telemetry-reading"] = new FakeEventHubsInspector(["0"], isGeoReplicationEnabled: true);
            var builder = TestFlowlyBuilder.Create().WithEventHubs(clientFactory);
            builder.AddMessageRecorder<TelemetryReading>();
            builder.Services.AddSingleton<MessageStreamCheckpoint<TelemetryReading>, FakeMessageStreamCheckpoint<TelemetryReading>>();
            var logger = new RecordingLogger<AzureEventHubsTopologyValidator>();

            await StartQueueRegistrar(builder, createTopology: false, logger);

            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("geo-replicated"));
        }

        [Fact]
        public async Task WithACheckpointStoreRegisteredAfterTheProvider_StillWarnsAtStartup()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            clientFactory.Inspectors["telemetry-reading"] = new FakeEventHubsInspector(["0"], isGeoReplicationEnabled: true);
            var builder = TestFlowlyBuilder.Create().WithEventHubs(clientFactory);
            builder.Services.AddSingleton<MessageStreamCheckpoint<TelemetryReading>, FakeMessageStreamCheckpoint<TelemetryReading>>();
            builder.AddMessageRecorder<TelemetryReading>();
            var logger = new RecordingLogger<AzureEventHubsTopologyValidator>();

            await StartQueueRegistrar(builder, createTopology: false, logger);

            Assert.Contains(logger.Entries, entry => entry.Message.Contains("geo-replicated"));
        }

        [Fact]
        public async Task WithAGeoReplicatedEventHubAndNoCheckpointStore_DoesNotWarn()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            clientFactory.Inspectors["telemetry-reading"] = new FakeEventHubsInspector(["0"], isGeoReplicationEnabled: true);
            var builder = TestFlowlyBuilder.Create().WithEventHubs(clientFactory);
            builder.AddMessageRecorder<TelemetryReading>();
            var logger = new RecordingLogger<AzureEventHubsTopologyValidator>();

            await StartQueueRegistrar(builder, createTopology: false, logger);

            Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("geo-replicated"));
        }

        private static async Task StartQueueRegistrar(IFlowlyBuilder builder, bool createTopology, ILogger<AzureEventHubsTopologyValidator>? validatorLogger = null)
        {
            builder.Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            if (validatorLogger is not null)
                builder.Services.AddSingleton(validatorLogger);
            builder.Services.AddSingleton<ICrossProviderConflictValidator, CrossProviderConflictValidator>();
            await using var serviceProvider = builder.Services.BuildServiceProvider();

            await new QueueRegistrarHostedService(
                serviceProvider.GetServices<ProviderQueueManifest>(),
                serviceProvider.GetRequiredService<IMessageBusClientRegistry>(),
                serviceProvider.GetRequiredService<IMessagingTopologyCreatorRegistry>(),
                serviceProvider.GetRequiredService<IEventTopologyCreatorRegistry>(),
                serviceProvider.GetServices<IMessagingTopologyValidator>(),
                serviceProvider.GetRequiredService<ICrossProviderConflictValidator>(),
                new FlowlyOptions { CreateTopology = createTopology },
                serviceProvider.GetRequiredService<ILogger<QueueRegistrarHostedService>>()).StartAsync(CancellationToken.None);
        }
    }

    public class Discovery
    {
        [Fact]
        public void WithPlaceholderConfiguration_RegistersWithoutParsingTheConnectionString()
        {
            var manifests = FlowlyDesignTimeFactory.DiscoverQueues(typeof(DiscoveryConfiguration));

            Assert.Equal("azure-event-hubs", Assert.Single(manifests).ProviderName);
        }
    }

    private sealed class DiscoveryConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public void Configure(IFlowlyBuilder builder)
            => builder.UseAzureEventHubs("EventHubs").AddMessageRecorder<TelemetryReading>();
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => throw new NotSupportedException();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private record TelemetryReading;

    [StreamPartitions(4)]
    private record PartitionedTelemetryReading;

    [ProviderAffinity("azure-event-hubs")]
    private record AffineTelemetryReading;
}
