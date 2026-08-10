using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

    public class AddMessageRecorder
    {
        [Fact]
        public void RegistersMessageSubmitterForMessageType()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageRecorder<TelemetryReading>();

            var descriptor = flowlyBuilder.Services.FirstOrDefault(s => s.ServiceType == typeof(IMessageSubmitter<TelemetryReading>));
            Assert.NotNull(descriptor);
            Assert.Equal(typeof(MessageSubmitter<TelemetryReading>), descriptor.ImplementationType);
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

    private sealed class StreamCapableStubClient : StubMessageBusClient, IStreamCapableMessageBusClient
    {
        public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
            => throw new NotImplementedException();
    }
}
