using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.Tests;

public class MessageStreamHandlerRegistrationExtensionsTests
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

    public class AddMessageStreamHandler
    {
        [Fact]
        public void RegistersHandlerAsScoped()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageStreamHandler<TelemetryReading, TelemetryReadingHandler>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(TelemetryReadingHandler));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            Assert.Equal(typeof(TelemetryReadingHandler), descriptor.ImplementationType);
        }

        [Fact]
        public void RegistersHandlerSettingsSingleton()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageStreamHandler<TelemetryReading, TelemetryReadingHandler>();

            var handlerSettings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(MessageStreamHandlerSettings<TelemetryReading, TelemetryReadingHandler>))
                .Select(s => (MessageStreamHandlerSettings<TelemetryReading, TelemetryReadingHandler>)s.ImplementationInstance!)
                .Single();

            Assert.Equal("telemetry-reading", handlerSettings.QueueName);
            Assert.Equal("primary", handlerSettings.ProviderName);
            Assert.Equal(nameof(TelemetryReadingHandler), handlerSettings.HandlerName);
            Assert.Equal(StartPosition.First(), handlerSettings.StartPosition);
        }

        [Fact]
        public void RegistersStreamProcessingBackgroundService()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageStreamHandler<TelemetryReading, TelemetryReadingHandler>();

            var descriptor = flowlyBuilder.Services.FirstOrDefault(s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(MessageStreamProcessingBackgroundService<TelemetryReading, TelemetryReadingHandler>));

            Assert.NotNull(descriptor);
        }

        [Fact]
        public void WhenClientIsNotStreamCapable_Throws()
        {
            var flowlyBuilder = CreateBuilder("primary", streamCapable: false);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                flowlyBuilder.AddMessageStreamHandler<TelemetryReading, TelemetryReadingHandler>());

            Assert.Contains("primary", exception.Message);
            Assert.Contains(nameof(IStreamCapableMessageBusClient), exception.Message);
        }

        [Fact]
        public void WhenHandlerDoesNotSetStartPosition_Throws()
        {
            var flowlyBuilder = CreateBuilder("primary");

            var exception = Assert.Throws<InvalidOperationException>(() =>
                flowlyBuilder.AddMessageStreamHandler<TelemetryReading, HandlerWithoutStartPosition>());

            Assert.Contains("StartPosition", exception.Message);
        }

        [Fact]
        public void MarksQueueAsStreamWithRetentionFromContract()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageStreamHandler<RetainedReading, RetainedReadingHandler>();

            var streamQueueManifest = StreamQueueManifest.GetOrCreate(flowlyBuilder.Services);
            Assert.True(streamQueueManifest.TryGetRetention("retained-reading", out var retention));
            Assert.Equal(3600, retention.MaxAgeSeconds);
        }

        [Fact]
        public void RegistersQueueInProviderManifest()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageStreamHandler<TelemetryReading, TelemetryReadingHandler>();

            var providerManifest = flowlyBuilder.Services
                .Where(s => s.ImplementationInstance is ProviderQueueManifest)
                .Select(s => (ProviderQueueManifest)s.ImplementationInstance!)
                .Single();

            Assert.Contains(providerManifest.Queues, q => q.QueueName == "telemetry-reading");
        }

        [Fact]
        public void ReturnsTheBuilder_ForFluentChaining()
        {
            var flowlyBuilder = CreateBuilder("primary");

            var returned = flowlyBuilder.AddMessageStreamHandler<TelemetryReading, TelemetryReadingHandler>();

            Assert.Same(flowlyBuilder, returned);
        }

        [Fact]
        public void WhenTwoHandlersRegisteredForSameMessageType_EachGetsIndependentRegistrations()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageStreamHandler<TelemetryReading, TelemetryReadingHandler>();
            flowlyBuilder.AddMessageStreamHandler<TelemetryReading, TelemetryReadingHandlerTwo>();

            Assert.Single(flowlyBuilder.Services, s => s.ServiceType == typeof(TelemetryReadingHandler));
            Assert.Single(flowlyBuilder.Services, s => s.ServiceType == typeof(TelemetryReadingHandlerTwo));

            var settingsOne = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(MessageStreamHandlerSettings<TelemetryReading, TelemetryReadingHandler>))
                .Select(s => (MessageStreamHandlerSettings<TelemetryReading, TelemetryReadingHandler>)s.ImplementationInstance!)
                .Single();

            var settingsTwo = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(MessageStreamHandlerSettings<TelemetryReading, TelemetryReadingHandlerTwo>))
                .Select(s => (MessageStreamHandlerSettings<TelemetryReading, TelemetryReadingHandlerTwo>)s.ImplementationInstance!)
                .Single();

            Assert.Equal(nameof(TelemetryReadingHandler), settingsOne.HandlerName);
            Assert.Equal(nameof(TelemetryReadingHandlerTwo), settingsTwo.HandlerName);

            Assert.Contains(flowlyBuilder.Services, s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(MessageStreamProcessingBackgroundService<TelemetryReading, TelemetryReadingHandler>));

            Assert.Contains(flowlyBuilder.Services, s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(MessageStreamProcessingBackgroundService<TelemetryReading, TelemetryReadingHandlerTwo>));
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private record TelemetryReading;

    [StreamRetention(maxAgeSeconds: 3600)]
    private record RetainedReading;

    private class TelemetryReadingHandler : MessageStreamHandler<TelemetryReading>
    {
        public override void Configure(MessageStreamHandlerOptions options) => options.StartPosition = StartPosition.First();

        public override Task Handle(IMessageStreamContext<TelemetryReading> messageContext) => Task.CompletedTask;
    }

    private class TelemetryReadingHandlerTwo : MessageStreamHandler<TelemetryReading>
    {
        public override void Configure(MessageStreamHandlerOptions options) => options.StartPosition = StartPosition.Last();

        public override Task Handle(IMessageStreamContext<TelemetryReading> messageContext) => Task.CompletedTask;
    }

    private class HandlerWithoutStartPosition : MessageStreamHandler<TelemetryReading>
    {
        public override Task Handle(IMessageStreamContext<TelemetryReading> messageContext) => Task.CompletedTask;
    }

    private class RetainedReadingHandler : MessageStreamHandler<RetainedReading>
    {
        public override void Configure(MessageStreamHandlerOptions options) => options.StartPosition = StartPosition.First();

        public override Task Handle(IMessageStreamContext<RetainedReading> messageContext) => Task.CompletedTask;
    }

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
