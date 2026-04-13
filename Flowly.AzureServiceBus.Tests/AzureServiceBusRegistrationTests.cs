using Azure.Core;
using Flowly.AzureServiceBus;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.AzureServiceBus.Tests;

public class AzureServiceBusRegistrationTests
{
    public class UseAzureServiceBusWithTokenCredential
    {
        [Fact]
        public void RegistersProviderAsPrimary()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseAzureServiceBus("testnamespace.servicebus.windows.net", new FakeTokenCredential());

            Assert.Single(registry.GetAll());
            Assert.True(registry.GetAll()[0].IsPrimary);
        }

        [Fact]
        public void WithExplicitName_RegistersWithThatName()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseAzureServiceBus(
                "testnamespace.servicebus.windows.net",
                new FakeTokenCredential(),
                name: "Primary");

            Assert.Equal("Primary", registry.GetAll()[0].Name);
        }

        [Fact]
        public void WithConfigKey_ResolvesNamespaceFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ServiceBusNamespace"] = "configured.servicebus.windows.net"
                })
                .Build();

            var (builder, registry) = CreateBuilder(configuration);

            builder.UseAzureServiceBus("ServiceBusNamespace", new FakeTokenCredential());

            Assert.Single(registry.GetAll());
        }

        [Fact]
        public void WithRawNamespace_UsesStringDirectlyWhenNotInConfiguration()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseAzureServiceBus("testnamespace.servicebus.windows.net", new FakeTokenCredential());

            Assert.Single(registry.GetAll());
        }
    }

    public class UseAzureServiceBusMultipleProviders
    {
        [Fact]
        public void SecondCallWithoutName_ThrowsInvalidOperationException()
        {
            var (builder, _) = CreateBuilder();
            builder.UseAzureServiceBus("testnamespace.servicebus.windows.net", new FakeTokenCredential());

            Assert.Throws<InvalidOperationException>(() =>
                builder.UseAzureServiceBus("testnamespace.servicebus.windows.net", new FakeTokenCredential()));
        }

        [Fact]
        public void SecondCallWithName_RegistersBothProviders()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseAzureServiceBus("testnamespace.servicebus.windows.net", new FakeTokenCredential());
            builder.UseAzureServiceBus(
                "testnamespace.servicebus.windows.net",
                new FakeTokenCredential(),
                name: "Secondary");

            Assert.Equal(2, registry.GetAll().Count);
        }
    }

    private static (IFlowlyBuilder builder, FakeMessageBusClientRegistry registry) CreateBuilder(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        var registry = new FakeMessageBusClientRegistry();
        var topologyRegistry = new FakeMessagingTopologyCreatorRegistry();

        services.AddSingleton<IMessageBusClientRegistry>(registry);
        services.AddSingleton<IMessagingTopologyCreatorRegistry>(topologyRegistry);

        var builder = new FakeFlowlyBuilder(services, configuration ?? new ConfigurationBuilder().Build());

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

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new AccessToken("fake-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => ValueTask.FromResult(new AccessToken("fake-token", DateTimeOffset.UtcNow.AddHours(1)));
    }
}
