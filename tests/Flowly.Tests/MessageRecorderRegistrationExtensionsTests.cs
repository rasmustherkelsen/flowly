using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.SendSteps;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Flowly.Tests;

public class MessageRecorderRegistrationExtensionsTests
{
    private static IFlowlyBuilder CreateBuilder(string providerName, bool streamCapable = true)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(providerName, streamCapable ? new StreamCapableStubClient() : new StubMessageBusClient(), null);
        services.AddSingleton<IMessageBusClientRegistry>(registry);
        services.AddSingleton(new ProviderQueueManifest(providerName, true, "Stub"));

        return new StubFlowlyBuilder(services);
    }

    private static IMessageSubmitter<TMessage> ResolveSubmitter<TMessage>(IFlowlyBuilder flowlyBuilder)
    {
        flowlyBuilder.Services.AddSingleton<ISubmitterInstrumentation, NullSubmitterInstrumentation>();
        flowlyBuilder.Services.TryAddSingleton<ISendStepRunner>(SendStepRunner.None);

        return flowlyBuilder.Services.BuildServiceProvider().GetRequiredService<IMessageSubmitter<TMessage>>();
    }

    private static IFlowlyBuilder CreateStreamOnlyBuilder(string providerName, FakeStreamOnlyClient? client = null)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(providerName, client ?? new FakeStreamOnlyClient(), null);
        services.AddSingleton<IMessageBusClientRegistry>(registry);
        services.AddSingleton(new ProviderQueueManifest(providerName, true, "Stub"));

        return new StubFlowlyBuilder(services);
    }

    public class AddMessageRecorder
    {
        [Fact]
        public void WithStreamOnlyProvider_RegistersMessageSubmitterForMessageType()
        {
            var flowlyBuilder = CreateStreamOnlyBuilder("streams");

            flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            Assert.Contains(flowlyBuilder.Services, s => s.ServiceType == typeof(IMessageSubmitter<TelemetryReading>));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WithQueueOnlyAndStreamOnlyProvidersInEitherOrder_RoutesStreamToStreamOnlyProvider(bool queueProviderFirst)
        {
            var services = new ServiceCollection();
            var registry = new MessageBusClientRegistry();
            var streamsManifest = new ProviderQueueManifest("streams", false, "Stub");
            var queuesManifest = new ProviderQueueManifest("queues", true, "Stub");
            services.AddSingleton<IMessageBusClientRegistry>(registry);
            services.AddSingleton(streamsManifest);
            services.AddSingleton(queuesManifest);
            var streamsClient = new FakeStreamOnlyClient(new FakeMessageBusSender());
            var providers = new (string Name, ITransportClient Client)[] { ("queues", new StubMessageBusClient()), ("streams", streamsClient) };

            foreach (var provider in queueProviderFirst ? providers : providers.Reverse())
            {
                registry.Register(provider.Name, provider.Client, null);
            }

            var flowlyBuilder = new StubFlowlyBuilder(services);

            flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            await ResolveSubmitter<TelemetryReading>(flowlyBuilder).Submit(new TelemetryReading());
            Assert.Equal("telemetry-reading", streamsClient.CreatedStreamSenderForQueue);
            Assert.Empty(queuesManifest.Queues);
            Assert.Single(streamsManifest.Queues);
        }

        [Fact]
        public async Task RegisteredSubmitterSendsThroughStreamSender()
        {
            var streamClient = new FakeStreamOnlyClient(new FakeMessageBusSender());
            var flowlyBuilder = CreateStreamOnlyBuilder("primary", streamClient);

            flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            await ResolveSubmitter<TelemetryReading>(flowlyBuilder).Submit(new TelemetryReading());
            Assert.Equal("telemetry-reading", streamClient.CreatedStreamSenderForQueue);
        }

        [Fact]
        public async Task RegisteredSubmitterRunsSendStepsFromTheContainer()
        {
            var flowlyBuilder = CreateBuilder("primary");
            var sendStepRunner = new CountingSendStepRunner();
            flowlyBuilder.Services.AddSingleton<ISendStepRunner>(sendStepRunner);
            flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            await ResolveSubmitter<TelemetryReading>(flowlyBuilder).Submit(new TelemetryReading());

            Assert.Equal(1, sendStepRunner.Runs);
        }

        [Fact]
        public void RegistersMessageSubmitterForMessageType()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            var descriptor = flowlyBuilder.Services.FirstOrDefault(s => s.ServiceType == typeof(IMessageSubmitter<TelemetryReading>));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.IsType<MessageSubmitter<TelemetryReading>>(ResolveSubmitter<TelemetryReading>(flowlyBuilder));
        }

        [Fact]
        public void AddsMessageRecorderAsSingleton()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            var descriptor = flowlyBuilder.Services.FirstOrDefault(s => s.ServiceType == typeof(IMessageRecorder));
            Assert.NotNull(descriptor);
            Assert.Equal(typeof(MessageRecorder), descriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void WhenClientIsNotStreamCapable_Throws()
        {
            var flowlyBuilder = CreateBuilder("primary", streamCapable: false);

            var exception = Assert.Throws<InvalidOperationException>(() => flowlyBuilder.AddMessageRecorder<TelemetryReading>());

            Assert.Contains("primary", exception.Message);
            Assert.Contains(nameof(IStreamCapableMessageBusClient), exception.Message);
        }

        [Fact]
        public void MarksQueueAsStreamWithRetentionFromContract()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageRecorder<RetainedReading>();

            var streamQueueManifest = StreamQueueManifest.GetOrCreate(flowlyBuilder.Services);
            Assert.True(streamQueueManifest.TryGetRetention("retained-reading", out var retention));
            Assert.Equal(3600, retention.MaxAgeSeconds);
            Assert.Equal(9_000_000, retention.MaxLengthBytes);
        }

        [Fact]
        public void AddsSubmitterManifestEntryWithStreamKind()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            var manifest = FlowlySubmitterManifest.GetOrCreate(flowlyBuilder.Services);
            var registration = Assert.Single(manifest.Submitters);
            Assert.Equal(typeof(TelemetryReading), registration.MessageType);
            Assert.Equal("telemetry-reading", registration.QueueOrTopicName);
            Assert.Equal(SubmitterKind.Stream, registration.Kind);
        }

        [Fact]
        public void RegistersQueueInProviderManifest()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            var providerManifest = flowlyBuilder.Services
                .Where(s => s.ImplementationInstance is ProviderQueueManifest)
                .Select(s => (ProviderQueueManifest)s.ImplementationInstance!)
                .Single();

            Assert.Contains(providerManifest.Queues, q => q.QueueName == "telemetry-reading");
        }

        [Fact]
        public void RegisteringSameMessageTypeTwice_DoesNotAddDuplicate()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageRecorder<TelemetryReading>();
            flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            var count = flowlyBuilder.Services.Count(s => s.ServiceType == typeof(IMessageSubmitter<TelemetryReading>));
            Assert.Equal(1, count);
        }

        [Fact]
        public void ReturnsTheBuilder_ForFluentChaining()
        {
            var flowlyBuilder = CreateBuilder("primary");

            var returned = flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            Assert.Same(flowlyBuilder, returned);
        }

        [Fact]
        public void MarksQueueAsStreamWithPartitionCountFromContract()
        {
            var flowlyBuilder = CreatePartitionedBuilder("primary");

            flowlyBuilder.AddMessageRecorder<PartitionedReading>();

            var streamQueueManifest = StreamQueueManifest.GetOrCreate(flowlyBuilder.Services);
            Assert.Equal(4, streamQueueManifest.GetPartitionCount("partitioned-reading"));
        }

        [Fact]
        public void WhenStreamPartitionsButClientNotPartitionedCapable_Throws()
        {
            var flowlyBuilder = CreatePartitionedBuilder("primary", partitionedCapable: false);

            var exception = Assert.Throws<InvalidOperationException>(() => flowlyBuilder.AddMessageRecorder<PartitionedReading>());

            Assert.Contains(nameof(IPartitionedStreamCapableMessageBusClient), exception.Message);
            Assert.Contains(nameof(PartitionedReading), exception.Message);
            Assert.Contains("[StreamPartitions]", exception.Message);
            Assert.Contains("'primary' does not support partitioned message streaming", exception.Message);
            Assert.Contains("primary (queue-based messaging, message streaming)", exception.Message);
        }
    }

    private static IFlowlyBuilder CreatePartitionedBuilder(string providerName, bool partitionedCapable = true)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(providerName, partitionedCapable ? new PartitionedStreamCapableStubClient() : new StreamCapableStubClient(), null);
        services.AddSingleton<IMessageBusClientRegistry>(registry);
        services.AddSingleton(new ProviderQueueManifest(providerName, true, "Stub"));

        return new StubFlowlyBuilder(services);
    }

    private sealed class CountingSendStepRunner : ISendStepRunner
    {
        public int Runs { get; private set; }

        public Task Run(object message, CancellationToken cancellationToken, SendStepDelegate send)
        {
            Runs++;

            return Task.CompletedTask;
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private record TelemetryReading;

    [StreamRetention(maxAgeSeconds: 3600, maxLengthBytes: 9_000_000)]
    private record RetainedReading;

    private class StubMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "Stub";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class StreamCapableStubClient : StubMessageBusClient, IStreamCapableMessageBusClient
    {
        public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
            => throw new NotImplementedException();
    }

    [StreamPartitions(4)]
    private record PartitionedReading;

    private sealed class PartitionedStreamCapableStubClient : StreamCapableStubClient, IPartitionedStreamCapableMessageBusClient
    {
        public Task<IPartitionedStreamConsumer<TMessage>> CreatePartitionedStreamConsumer<TMessage>(
            string queueName, int partitionCount, Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition, MessageBusProcessorOptions options,
            ILogger logger)
            => throw new NotImplementedException();
    }
}
