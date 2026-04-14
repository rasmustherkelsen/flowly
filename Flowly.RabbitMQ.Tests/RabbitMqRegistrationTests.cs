using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqRegistrationTests
{
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

    private static (IFlowlyBuilder builder, FakeMessageBusClientRegistry registry) CreateBuilder()
    {
        var services = new ServiceCollection();
        var registry = new FakeMessageBusClientRegistry();
        var topologyRegistry = new FakeMessagingTopologyCreatorRegistry();

        services.AddSingleton<IMessageBusClientRegistry>(registry);
        services.AddSingleton<IMessagingTopologyCreatorRegistry>(topologyRegistry);

        var builder = new FakeFlowlyBuilder(services, new ConfigurationBuilder().Build());

        return (builder, registry);
    }

    private sealed class FakeFlowlyBuilder(IServiceCollection services, IConfiguration configuration) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => configuration;
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

        public bool IsRegistered(string providerName) =>
            _transports.Any(t => string.Equals(t.Name, providerName, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<RegisteredTransport> GetAll() => _transports;
    }

    private sealed class FakeMessagingTopologyCreatorRegistry : IMessagingTopologyCreatorRegistry
    {
        public void Register(string providerName, IMessagingTopologyCreator creator) { }
        public IMessagingTopologyCreator GetCreator(string providerName) => throw new NotImplementedException();
    }
}
