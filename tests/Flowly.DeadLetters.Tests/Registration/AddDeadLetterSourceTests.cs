using Flowly.DeadLetters.BackgroundServices;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.DeadLetters.Tests.Registration;

public class AddDeadLetterSourceTests
{
    private static IFlowlyBuilder BuildFlowlyBuilder(ServiceCollection? services = null)
    {
        services ??= new ServiceCollection();
        services.AddSingleton<IMessageBusClientRegistry>(new StubMessageBusClientRegistry("primary"));
        return new StubFlowlyBuilder(services);
    }

    private static IFlowlyBuilder BuildFlowlyBuilderWithDeadLetterTracking(ServiceCollection? services = null)
    {
        var flowlyBuilder = BuildFlowlyBuilder(services);
        flowlyBuilder.AddDeadLetterTracking(_ => { });
        return flowlyBuilder;
    }

    public class WhenDeadLetterTrackingNotConfigured
    {
        [Fact]
        public void ThrowsInvalidOperationException()
        {
            var flowlyBuilder = BuildFlowlyBuilder();

            Assert.Throws<InvalidOperationException>(() => flowlyBuilder.AddDeadLetterSource<OrderMessage>());
        }
    }

    public class WhenDeadLetterTrackingIsConfigured
    {
        [Fact]
        public void RegistersIngestionSettingsWithCorrectQueueName()
        {
            var flowlyBuilder = BuildFlowlyBuilderWithDeadLetterTracking();

            flowlyBuilder.AddDeadLetterSource<OrderMessage>();

            var settings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(DeadLetterIngestionSettings))
                .Select(s => s.ImplementationInstance)
                .OfType<DeadLetterIngestionSettings>()
                .FirstOrDefault();

            Assert.NotNull(settings);
            Assert.Equal("order", settings.QueueName);
        }

        [Fact]
        public void RegistersIngestionSettingsWithCorrectProviderName()
        {
            var flowlyBuilder = BuildFlowlyBuilderWithDeadLetterTracking();

            flowlyBuilder.AddDeadLetterSource<OrderMessage>();

            var settings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(DeadLetterIngestionSettings))
                .Select(s => s.ImplementationInstance)
                .OfType<DeadLetterIngestionSettings>()
                .FirstOrDefault();

            Assert.NotNull(settings);
            Assert.Equal("primary", settings.ProviderName);
        }

        [Fact]
        public void RegistersIngestionBackgroundService()
        {
            var flowlyBuilder = BuildFlowlyBuilderWithDeadLetterTracking();

            flowlyBuilder.AddDeadLetterSource<OrderMessage>();

            var descriptor = flowlyBuilder.Services.FirstOrDefault(s =>
                s.ImplementationType == typeof(DeadLetterIngestionBackgroundService));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void CallingTwiceForSameMessage_DoesNotRegisterDuplicate()
        {
            var flowlyBuilder = BuildFlowlyBuilderWithDeadLetterTracking();

            flowlyBuilder.AddDeadLetterSource<OrderMessage>();
            flowlyBuilder.AddDeadLetterSource<OrderMessage>();

            var count = flowlyBuilder.Services
                .Count(s => s.ServiceType == typeof(DeadLetterIngestionSettings));

            Assert.Equal(1, count);
        }

        [Fact]
        public void TwoDistinctMessages_BothRegistered()
        {
            var flowlyBuilder = BuildFlowlyBuilderWithDeadLetterTracking();

            flowlyBuilder.AddDeadLetterSource<OrderMessage>();
            flowlyBuilder.AddDeadLetterSource<PaymentMessage>();

            var count = flowlyBuilder.Services
                .Count(s => s.ServiceType == typeof(DeadLetterIngestionSettings));

            Assert.Equal(2, count);
        }

        [Fact]
        public void RespectsQueueNameAttribute()
        {
            var flowlyBuilder = BuildFlowlyBuilderWithDeadLetterTracking();

            flowlyBuilder.AddDeadLetterSource<CustomQueueMessage>();

            var settings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(DeadLetterIngestionSettings))
                .Select(s => s.ImplementationInstance)
                .OfType<DeadLetterIngestionSettings>()
                .FirstOrDefault();

            Assert.NotNull(settings);
            Assert.Equal("custom-queue", settings.QueueName);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private sealed class StubMessageBusClientRegistry(string primaryProviderName) : IMessageBusClientRegistry
    {
        public string PrimaryProviderName => primaryProviderName;

        public IMessageBusClient GetClient(string providerName) => throw new NotImplementedException();
        public bool IsRegistered(string providerName) => true;
        public IReadOnlyList<RegisteredTransport> GetAll() => [new RegisteredTransport(primaryProviderName, true, null)];
        public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride) { }
    }

    private record OrderMessage;
    private record PaymentMessage;

    [QueueName("custom-queue")]
    private record CustomQueueMessage;
}
