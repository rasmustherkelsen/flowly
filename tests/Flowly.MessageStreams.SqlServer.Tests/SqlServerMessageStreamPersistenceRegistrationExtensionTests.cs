using Flowly.MessageStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageStreams.SqlServer.Tests;

public class SqlServerMessageStreamPersistenceRegistrationExtensionTests
{
    public class AddSqlServerMessageStreamPersistence
    {
        [Fact]
        public void WithConnectionStringName_ResolvesConnectionStringFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MsgStreamDb"] = "Data Source=cfg-server;Initial Catalog=cfg-db"
                })
                .Build();
            var builder = new StubFlowlyBuilder(new ServiceCollection(), configuration);

            builder.AddSqlServerMessageStreamPersistence("MsgStreamDb", enableMigrations: false);

            Assert.Contains("Initial Catalog=cfg-db", ResolveConnectionString(builder.Services, null));
        }

        [Fact]
        public void WithLiteralConnectionString_UsesValueDirectly()
        {
            var builder = new StubFlowlyBuilder(new ServiceCollection(), new ConfigurationBuilder().Build());

            builder.AddSqlServerMessageStreamPersistence("Data Source=direct-server;Initial Catalog=direct-db", enableMigrations: false);

            Assert.Contains("Initial Catalog=direct-db", ResolveConnectionString(builder.Services, null));
        }

        [Fact]
        public void WithName_RegistersUnderThatName()
        {
            var builder = new StubFlowlyBuilder(new ServiceCollection(), new ConfigurationBuilder().Build());
            builder.AddSqlServerMessageStreamPersistence("Data Source=default;Initial Catalog=default", enableMigrations: false);

            builder.AddSqlServerMessageStreamPersistence("Data Source=named;Initial Catalog=named", name: "secondary", enableMigrations: false);

            Assert.Contains("Initial Catalog=named", ResolveConnectionString(builder.Services, "secondary"));
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
