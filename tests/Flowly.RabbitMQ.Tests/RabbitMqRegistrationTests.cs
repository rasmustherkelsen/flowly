using Flowly.MessageInfrastructure.Registration;
using Flowly.RabbitMQ.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:RabbitMQ"] = "amqp://configured-host:5672/" })
            .Build();

        var builder = new FakeFlowlyBuilder(services, configuration);

        return (builder, registry);
    }

    public class UseRabbitMq
    {
        [Fact]
        public void RegistersTopologyCreatorForTheProvider()
        {
            var (builder, _) = CreateBuilder();

            builder.UseRabbitMq();

            var topologyRegistry = builder.Services.Select(service => service.ImplementationInstance).OfType<FakeMessagingTopologyCreatorRegistry>().Single();
            Assert.NotNull(topologyRegistry.Creators["rabbitmq"]);
        }

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

        [Fact]
        public void RegistersStreamCapableClient()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq();

            Assert.IsAssignableFrom<IStreamCapableMessageBusClient>(Assert.Single(registry.Clients));
        }

        [Fact]
        public void RegistersStreamQueueManifestSingleton()
        {
            var (builder, _) = CreateBuilder();

            builder.UseRabbitMq();

            Assert.Single(builder.Services, s => s.ImplementationInstance is StreamQueueManifest);
        }

        [Fact]
        public void WithExplicitStreamPort_RegistersSuccessfully()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq(streamPort: 15552);

            Assert.Single(registry.GetAll());
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

    public class UseRabbitMqWithSinglePositionalString
    {
        [Fact]
        public void BindsTheStringToTheConnection()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("RabbitMQ");

            Assert.Equal("configured-host", RegisteredHost(builder));
            Assert.Equal("rabbitmq", Assert.Single(registry.GetAll()).Name);
        }

        [Fact]
        public void WithEnableHealthCheck_BindsTheStringToTheConnectionAndRegistersTheHealthCheck()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("RabbitMQ", enableHealthCheck: true);

            Assert.Equal("configured-host", RegisteredHost(builder));
            Assert.Equal("rabbitmq", Assert.Single(registry.GetAll()).Name);
            Assert.Contains(builder.Services, service => service.ServiceType == typeof(HealthCheckService));
        }

        [Fact]
        public void WithCreateTopology_BindsTheStringToTheConnectionAndForwardsCreateTopology()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("RabbitMQ", createTopology: true);

            Assert.Equal("configured-host", RegisteredHost(builder));
            Assert.True(Assert.Single(registry.GetAll()).CreateTopologyOverride);
        }

        [Fact]
        public void WithStreamPort_BindsTheStringToTheConnectionAndForwardsTheStreamPort()
        {
            var (builder, _) = CreateBuilder();

            builder.UseRabbitMq("RabbitMQ", streamPort: 15552);

            Assert.Equal("configured-host", RegisteredHost(builder));
            Assert.Equal(15552, RegisteredStreamPort(builder));
        }

        [Fact]
        public void WithoutMatchingConnectionString_UsesTheStringAsTheConnectionUri()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("amqp://literal-host:5672/");

            Assert.Equal("literal-host", RegisteredHost(builder));
            Assert.Equal("rabbitmq", Assert.Single(registry.GetAll()).Name);
        }

        [Fact]
        public void WithNameArgument_BindsTheStringToTheConnectionAndTheNameToTheProvider()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("RabbitMQ", name: "secondary");

            Assert.Equal("configured-host", RegisteredHost(builder));
            Assert.Equal("secondary", Assert.Single(registry.GetAll()).Name);
        }

        [Fact]
        public void WithConnectionNamedArgument_BindsTheStringToTheConnection()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq(connection: "RabbitMQ");

            Assert.Equal("configured-host", RegisteredHost(builder));
            Assert.Equal("rabbitmq", Assert.Single(registry.GetAll()).Name);
        }

        [Fact]
        public void WithSecondProvider_RegistersBothProviders()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("RabbitMQ");
            builder.UseRabbitMq("amqp://literal-host:5672/", name: "secondary");

            Assert.Equal(["rabbitmq", "secondary"], registry.GetAll().Select(transport => transport.Name));
        }
    }

    public class UseRabbitMqWithPositionalNameAndOptions
    {
        [Fact]
        public void StringAndBool_BindsTheStringToTheNameAndTheBoolToCreateTopology()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("secondary", true);

            var transport = Assert.Single(registry.GetAll());
            Assert.Equal("secondary", transport.Name);
            Assert.True(transport.CreateTopologyOverride);
            Assert.Equal("localhost", RegisteredHost(builder));
        }

        [Fact]
        public void StringAndTwoBools_BindsTheBoolsToCreateTopologyAndEnableHealthCheck()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("secondary", false, true);

            var transport = Assert.Single(registry.GetAll());
            Assert.Equal("secondary", transport.Name);
            Assert.False(transport.CreateTopologyOverride);
            Assert.Equal("localhost", RegisteredHost(builder));
            Assert.Contains(builder.Services, service => service.ServiceType == typeof(HealthCheckService));
        }

        [Fact]
        public void StringBoolAndNamedEnableHealthCheck_BindsTheStringToTheName()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("secondary", true, enableHealthCheck: true);

            var transport = Assert.Single(registry.GetAll());
            Assert.Equal("secondary", transport.Name);
            Assert.True(transport.CreateTopologyOverride);
            Assert.Equal("localhost", RegisteredHost(builder));
        }

        [Fact]
        public void NameNamedArgumentOnly_BindsToTheDefaultConnection()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq(name: "secondary");

            Assert.Equal("secondary", Assert.Single(registry.GetAll()).Name);
            Assert.Equal("localhost", RegisteredHost(builder));
        }
    }

    public class UseRabbitMqWithConnectionAndName
    {
        [Fact]
        public void TwoPositionalStrings_BindsTheFirstToTheConnectionAndTheSecondToTheName()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("RabbitMQ", "secondary");

            Assert.Equal("configured-host", RegisteredHost(builder));
            Assert.Equal("secondary", Assert.Single(registry.GetAll()).Name);
        }

        [Fact]
        public void TwoPositionalStringsAndCreateTopology_ForwardsCreateTopology()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq("RabbitMQ", "secondary", true);

            Assert.Equal("configured-host", RegisteredHost(builder));
            Assert.True(Assert.Single(registry.GetAll()).CreateTopologyOverride);
        }

        [Fact]
        public void NamedArgumentsInReverseOrder_BindsEachToItsParameter()
        {
            var (builder, registry) = CreateBuilder();

            builder.UseRabbitMq(name: "secondary", connection: "RabbitMQ");

            Assert.Equal("configured-host", RegisteredHost(builder));
            Assert.Equal("secondary", Assert.Single(registry.GetAll()).Name);
        }
    }

    private static string RegisteredHost(IFlowlyBuilder builder)
        => RegisteredConnectionPool(builder).ResolveStreamEndpoint().Host;

    private static int RegisteredStreamPort(IFlowlyBuilder builder)
        => RegisteredConnectionPool(builder).ResolveStreamEndpoint().Port;

    private static RabbitMqConnectionPool RegisteredConnectionPool(IFlowlyBuilder builder)
        => builder.Services.Select(service => service.ImplementationInstance).OfType<RabbitMqConnectionLifetime>().Single().ConnectionPool;
}
