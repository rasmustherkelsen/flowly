using Flowly.Jobs.DatabaseModel;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Flowly.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Postgres.Tests.Registration;

public class PostgresJobStateTrackerRegistrationExtensionTests
{
    public class AddPostgresJobStateTracking
    {
        [Fact]
        public void WithConnectionStringName_ResolvesConnectionStringFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:JobsDb"] = "Host=cfg;Database=jobs"
                })
                .Build();
            var builder = CreateFullBuilder(configuration);

            builder.AddPostgresJobStateTracking("JobsDb");

            Assert.Equal("Host=cfg;Database=jobs", ResolveConnectionString(builder.Services));
        }

        [Fact]
        public void WithLiteralConnectionString_UsesValueDirectly()
        {
            var builder = CreateFullBuilder();

            builder.AddPostgresJobStateTracking("Host=direct;Database=jobs");

            Assert.Equal("Host=direct;Database=jobs", ResolveConnectionString(builder.Services));
        }
    }

    public class AddJobStateTrackingClient
    {
        [Fact]
        public void WithConnectionStringName_ResolvesConnectionStringFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:JobsDb"] = "Host=cfg;Database=jobs"
                })
                .Build();
            var builder = CreateSimpleBuilder(configuration);

            builder.AddJobStateTrackingClient("JobsDb");

            Assert.Equal("Host=cfg;Database=jobs", ResolveConnectionString(builder.Services));
        }

        [Fact]
        public void WithLiteralConnectionString_UsesValueDirectly()
        {
            var builder = CreateSimpleBuilder();

            builder.AddJobStateTrackingClient("Host=direct;Database=jobs");

            Assert.Equal("Host=direct;Database=jobs", ResolveConnectionString(builder.Services));
        }
    }

    private static string? ResolveConnectionString(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DbContextOptions<JobStateDataContext>>();
        return options.Extensions.OfType<RelationalOptionsExtension>().First().ConnectionString;
    }

    private static StubFlowlyBuilder CreateSimpleBuilder(IConfiguration? configuration = null) =>
        new(new ServiceCollection(), configuration ?? new ConfigurationBuilder().Build());

    private static StubFlowlyBuilder CreateFullBuilder(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        var registry = new FakeMessageBusClientRegistry();
        services.AddSingleton<IMessageBusClientRegistry>(registry);
        services.AddSingleton(new ProviderQueueManifest("primary", true, "Fake"));
        services.AddSingleton<ITopologyNameResolver, KebabCaseTopologyNameResolver>();
        services.AddSingleton<IHandlerSettingsFactory, HandlerSettingsFactory>();
        services.AddSingleton<IQueueRegistrar, QueueRegistrar>();

        return new StubFlowlyBuilder(services, configuration ?? new ConfigurationBuilder().Build());
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services, IConfiguration configuration) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => configuration;
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private sealed class FakeMessageBusClientRegistry : IMessageBusClientRegistry
    {
        public string PrimaryProviderName => "primary";
        public void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride) { }
        public IMessageBusClient GetClient(string providerName) => throw new NotImplementedException();
        public bool IsRegistered(string providerName) => false;
        public IReadOnlyList<RegisteredTransport> GetAll() => [];
    }
}
