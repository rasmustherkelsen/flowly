using Flowly.MessageStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageStreams.Postgres.Tests;

public class PostgresMessageStreamPersistenceRegistrationExtensionTests
{
    public class AddPostgresMessageStreamPersistence
    {
        [Fact]
        public void WithConnectionStringName_ResolvesConnectionStringFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MsgStreamDb"] = "Host=cfg-host;Database=cfg-db"
                })
                .Build();
            var builder = new StubFlowlyBuilder(new ServiceCollection(), configuration);

            builder.AddPostgresMessageStreamPersistence("MsgStreamDb", enableMigrations: false);

            Assert.Contains("Database=cfg-db", ResolveConnectionString(builder.Services, null));
        }

        [Fact]
        public void WithLiteralConnectionString_UsesValueDirectly()
        {
            var builder = new StubFlowlyBuilder(new ServiceCollection(), new ConfigurationBuilder().Build());

            builder.AddPostgresMessageStreamPersistence("Host=direct-host;Database=direct-db", enableMigrations: false);

            Assert.Contains("Database=direct-db", ResolveConnectionString(builder.Services, null));
        }

        [Fact]
        public void WithName_RegistersUnderThatName()
        {
            var builder = new StubFlowlyBuilder(new ServiceCollection(), new ConfigurationBuilder().Build());
            builder.AddPostgresMessageStreamPersistence("Host=default;Database=default", enableMigrations: false);

            builder.AddPostgresMessageStreamPersistence("Host=named;Database=named", name: "secondary", enableMigrations: false);

            Assert.Contains("Database=named", ResolveConnectionString(builder.Services, "secondary"));
        }
    }

    private static string? ResolveConnectionString(IServiceCollection services, string? name)
    {
        var registry = services
            .Where(s => s.ServiceType == typeof(MessageStreamCheckpointStoreRegistry))
            .Select(s => s.ImplementationInstance)
            .OfType<MessageStreamCheckpointStoreRegistry>()
            .First();

        using var context = registry.Resolve(name).CreateDbContext();
        return context.Database.GetConnectionString();
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services, IConfiguration configuration) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => configuration;
        public ITopologyNameResolver TopologyNameResolver => new Flowly.MessageInfrastructure.KebabCaseTopologyNameResolver();
    }
}
