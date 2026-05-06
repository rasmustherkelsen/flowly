using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.MessageHandlingStrategies;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class MessageProcessingPipelineExtensionsTests
{
    public class AddMessageProcessingPipeline
    {
        [Fact]
        public void RegistersMessageProcessingBackgroundServiceAsHostedService()
        {
            var serviceCollection = CreateServiceCollection();
            var handlerSettings = CreateHandlerSettings();
            Func<IServiceProvider, IMessageHandlingStrategy<TestMessage>> strategyFactory = _ => new StandardMessageHandlingStrategy<TestMessage>();

            serviceCollection.AddMessageProcessingPipeline(handlerSettings, strategyFactory);

            Assert.Contains(serviceCollection, s => s.ServiceType == typeof(IHostedService));
        }

        [Fact]
        public void HostedServiceFactoryProducesMessageProcessingBackgroundService()
        {
            var serviceCollection = CreateServiceCollection();
            var handlerSettings = CreateHandlerSettings();
            Func<IServiceProvider, IMessageHandlingStrategy<TestMessage>> strategyFactory = _ => new StandardMessageHandlingStrategy<TestMessage>();

            serviceCollection.AddMessageProcessingPipeline(handlerSettings, strategyFactory);

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();

            Assert.Contains(hostedServices, hs => hs is MessageProcessingBackgroundService<TestMessage>);
        }

        [Fact]
        public void StrategyFactoryIsInvoked()
        {
            var serviceCollection = CreateServiceCollection();
            var handlerSettings = CreateHandlerSettings();
            var factoryInvoked = false;
            Func<IServiceProvider, IMessageHandlingStrategy<TestMessage>> strategyFactory = _ =>
            {
                factoryInvoked = true;
                return new StandardMessageHandlingStrategy<TestMessage>();
            };

            serviceCollection.AddMessageProcessingPipeline(handlerSettings, strategyFactory);

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            _ = serviceProvider.GetServices<IHostedService>().OfType<MessageProcessingBackgroundService<TestMessage>>().Single();

            Assert.True(factoryInvoked);
        }

        [Fact]
        public void ReturnsSameServiceCollection()
        {
            var serviceCollection = CreateServiceCollection();
            var handlerSettings = CreateHandlerSettings();
            Func<IServiceProvider, IMessageHandlingStrategy<TestMessage>> strategyFactory = _ => new StandardMessageHandlingStrategy<TestMessage>();

            var returned = serviceCollection.AddMessageProcessingPipeline(handlerSettings, strategyFactory);

            Assert.Same(serviceCollection, returned);
        }

        [Fact]
        public void RegistersHostedServiceUsingProvidedHandlerSettings()
        {
            var serviceCollection = CreateServiceCollection();
            var handlerSettings = new HandlerSettings<TestMessage>("specific-queue", "stub", "TestHandler", false, 1, 0, 0, 0, TimeSpan.Zero);
            Func<IServiceProvider, IMessageHandlingStrategy<TestMessage>> strategyFactory = _ => new StandardMessageHandlingStrategy<TestMessage>();

            serviceCollection.AddMessageProcessingPipeline(handlerSettings, strategyFactory);

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var hostedService = serviceProvider.GetServices<IHostedService>().OfType<MessageProcessingBackgroundService<TestMessage>>().Single();

            Assert.NotNull(hostedService);
        }
    }

    private static ServiceCollection CreateServiceCollection()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IMessageBusClientRegistry>(new StubClientRegistry());
        serviceCollection.AddSingleton<IServiceScopeFactory>(new StubServiceScopeFactory());
        serviceCollection.AddSingleton<IHandlerInstrumentation, NullHandlerInstrumentation>();
        serviceCollection.AddSingleton<ILogger<MessageProcessingBackgroundService<TestMessage>>>(NullLogger<MessageProcessingBackgroundService<TestMessage>>.Instance);
        return serviceCollection;
    }

    private static HandlerSettings<TestMessage> CreateHandlerSettings()
        => new("test-queue", "stub", "TestHandler", false, 1, 0, 0, 0, TimeSpan.Zero);

    private record TestMessage;

    private sealed class StubClientRegistry : IMessageBusClientRegistry
    {
        public string PrimaryProviderName => "stub";

        public IMessageBusClient GetClient(string providerName) => throw new NotSupportedException();
        public bool IsRegistered(string providerName) => true;
        public IReadOnlyList<RegisteredTransport> GetAll() => [new RegisteredTransport("stub", true, null)];
        public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride) { }
    }

    private sealed class StubServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException();
    }
}
