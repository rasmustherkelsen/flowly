using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqRegistrationTests
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

    public class UseRabbitMq
    {
        [Fact]
        public void RegistersProviderAsPrimary()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq();

            Assert.Single(registry.GetAll());
            Assert.True(registry.GetAll()[0].IsPrimary);
        }

        [Fact]
        public void WithoutName_RegistersWithDefaultProviderName()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq();

            Assert.Equal("rabbitmq", registry.GetAll()[0].Name);
        }

        [Fact]
        public void WithExplicitName_RegistersWithThatName()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq(name: "secondary");

            Assert.Equal("secondary", registry.GetAll()[0].Name);
        }
    }

    public class UseRabbitMqMultipleProviders
    {
        [Fact]
        public void SecondCallWithoutName_ThrowsInvalidOperationException()
        {
            var (builder, _) = CreateBuilder();
            builder.UseRabbitMq();

            Assert.Throws<InvalidOperationException>(() => builder.UseRabbitMq());
        }

        [Fact]
        public void SecondCallWithName_RegistersBothProviders()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq();
            builder.UseRabbitMq(name: "secondary");

            Assert.Equal(2, registry.GetAll().Count);
        }
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

        public IMessageBusClient GetClient(string providerName)
        {
            throw new NotImplementedException();
        }

        public bool IsRegistered(string providerName)
        {
            return _transports.Any(t => string.Equals(t.Name, providerName, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<RegisteredTransport> GetAll()
        {
            return _transports;
        }
    }

    private sealed class FakeMessagingTopologyCreatorRegistry : IMessagingTopologyCreatorRegistry
    {
        public void Register(string providerName, IMessagingTopologyCreator creator)
        {
        }

        public IMessagingTopologyCreator GetCreator(string providerName)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeEventTopologyCreatorRegistry : IEventTopologyCreatorRegistry
    {
        public void Register(string providerName, IEventTopologyCreator creator)
        {
        }

        public IEventTopologyCreator? TryGetCreator(string providerName)
        {
            return null;
        }
    }
}