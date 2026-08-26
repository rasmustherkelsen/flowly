using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Callers;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.Tests;

public class CallHandlerRegistrationExtensionsTests
{
    public class AddCallSubmitter
    {
        [Fact]
        public void WithoutInstanceName_Throws()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary", instanceName: null);

            var exception = Assert.Throws<InvalidOperationException>(
                () => flowlyBuilder.AddCallSubmitter<PingMessage>());

            Assert.Contains("InstanceName", exception.Message);
        }

        [Fact]
        public void WithInstanceName_RegistersIMessageCaller()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary", instanceName: "sender");

            flowlyBuilder.AddCallSubmitter<PingMessage>();

            Assert.Contains(flowlyBuilder.Services, s => s.ServiceType == typeof(IMessageCaller));
        }

        [Fact]
        public void WithInstanceName_RegistersCallReturnQueueBackgroundService()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary", instanceName: "sender");

            flowlyBuilder.AddCallSubmitter<PingMessage>();

            Assert.Contains(flowlyBuilder.Services, s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(CallReturnQueueBackgroundService<PongMessage>));
        }

        [Fact]
        public void WithInstanceName_RegistersReplyQueueInManifest()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary", instanceName: "sender");

            flowlyBuilder.AddCallSubmitter<PingMessage>();

            Assert.Contains(manifest.Queues, q => q.QueueName == "ping-reply-sender" && q.IsReplyQueue);
        }

        [Fact]
        public void WithInstanceName_RegistersCallQueueInManifest()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary", instanceName: "sender");

            flowlyBuilder.AddCallSubmitter<PingMessage>();

            Assert.Contains(manifest.Queues, q => q.QueueName == "ping" && !q.IsReplyQueue);
        }

        [Fact]
        public void CustomTimeout_IsAppliedToCallSubmitterSettings()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary", instanceName: "sender");

            flowlyBuilder.AddCallSubmitter<PingMessage>(opts => opts.Timeout = TimeSpan.FromSeconds(30));

            var settings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(CallSubmitter<PingMessage, PongMessage>.Settings))
                .Select(s => (CallSubmitter<PingMessage, PongMessage>.Settings)s.ImplementationInstance!)
                .Single();

            Assert.Equal(TimeSpan.FromSeconds(30), settings.Timeout);
        }

        [Fact]
        public void WithoutCustomTimeout_FallsBackToGlobalDefault()
        {
            var globalTimeout = TimeSpan.FromMinutes(5);
            var (flowlyBuilder, _) = CreateBuilder("primary", instanceName: "sender", globalTimeout: globalTimeout);

            flowlyBuilder.AddCallSubmitter<PingMessage>();

            var settings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(CallSubmitter<PingMessage, PongMessage>.Settings))
                .Select(s => (CallSubmitter<PingMessage, PongMessage>.Settings)s.ImplementationInstance!)
                .Single();

            Assert.Equal(globalTimeout, settings.Timeout);
        }
    }

    public class AddCallHandler
    {
        [Fact]
        public void RegistersCallHandlerAsScoped()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary", instanceName: null);

            flowlyBuilder.AddCallHandler<PingMessage, PingCallHandler>();

            Assert.Contains(flowlyBuilder.Services, s =>
                s.ServiceType == typeof(CallHandler<PingMessage, PongMessage>) &&
                s.ImplementationType == typeof(PingCallHandler) &&
                s.Lifetime == ServiceLifetime.Scoped);
        }

        [Fact]
        public void RegistersMessageHandlingBackgroundService()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary", instanceName: null);

            flowlyBuilder.AddCallHandler<PingMessage, PingCallHandler>();

            Assert.Contains(flowlyBuilder.Services, s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(MessageProcessingBackgroundService<PingMessage>));
        }

        [Fact]
        public void WithMessageTypeThatDoesNotImplementIReturns_Throws()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary", instanceName: null);

            Assert.Throws<InvalidOperationException>(
                () => flowlyBuilder.AddCallHandler<PlainMessage, PlainCallHandler>());
        }
    }

    private static (IFlowlyBuilder FlowlyBuilder, ProviderQueueManifest Manifest) CreateBuilder(
        string providerName,
        string? instanceName,
        TimeSpan? globalTimeout = null)
    {
        var services = new ServiceCollection();

        var flowlyOptions = new FlowlyOptions
        {
            InstanceName = instanceName,
            MessageCallTimeout = globalTimeout ?? TimeSpan.FromMinutes(2)
        };
        services.AddSingleton(flowlyOptions);

        var registry = new MessageBusClientRegistry();
        registry.Register(providerName, new StubMessageBusClient(), null);
        services.AddSingleton<IMessageBusClientRegistry>(registry);

        var manifest = new ProviderQueueManifest(providerName, true, "Stub");
        services.AddSingleton(manifest);
        services.AddSingleton<ITopologyNameResolver, KebabCaseTopologyNameResolver>();
        services.AddSingleton<IHandlerSettingsFactory, HandlerSettingsFactory>();
        services.AddSingleton<IQueueRegistrar, QueueRegistrar>();

        return (new StubFlowlyBuilder(services), manifest);
    }

    private record PingMessage(string Payload) : IReturns<PongMessage>;
    private record PongMessage(string Echo);
    private record PlainMessage(string Text);

    private sealed class PingCallHandler : CallHandler<PingMessage, PongMessage>
    {
        protected override Task<PongMessage> Handle(IMessageContext<PingMessage> messageContext)
            => Task.FromResult(new PongMessage(messageContext.Message.Payload));
    }

    private sealed class PlainCallHandler : MessageHandler<PlainMessage>
    {
        public override Task Handle(IMessageContext<PlainMessage> messageContext) => Task.CompletedTask;
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private sealed class StubMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "Stub";
        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotSupportedException();
        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotSupportedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotSupportedException();
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotSupportedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
