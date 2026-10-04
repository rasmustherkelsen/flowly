using Flowly.MessageStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageStreams.SQLite.Tests;

public class SQLiteMessageStreamPersistenceRegistrationExtensionTests
{
    public class AddSQLiteMessageStreamPersistence
    {
        [Fact]
        public void WithConnectionStringName_ResolvesConnectionStringFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MsgStreamDb"] = "Data Source=cfg-message-streams.db"
                })
                .Build();
            var builder = new StubFlowlyBuilder(new ServiceCollection(), configuration);

            builder.AddSQLiteMessageStreamPersistence("MsgStreamDb", enableMigrations: false);

            Assert.Contains("cfg-message-streams.db", ResolveConnectionString(builder.Services, null));
        }

        [Fact]
        public void WithLiteralConnectionString_UsesValueDirectly()
        {
            var builder = new StubFlowlyBuilder(new ServiceCollection(), new ConfigurationBuilder().Build());

            builder.AddSQLiteMessageStreamPersistence("Data Source=direct-message-streams.db", enableMigrations: false);

            Assert.Contains("direct-message-streams.db", ResolveConnectionString(builder.Services, null));
        }

        [Fact]
        public void WithBareMemoryDataSource_Throws()
        {
            var builder = new StubFlowlyBuilder(new ServiceCollection(), new ConfigurationBuilder().Build());

            Assert.Throws<InvalidOperationException>(() =>
                builder.AddSQLiteMessageStreamPersistence("Data Source=:memory:", enableMigrations: false));
        }

        [Fact]
        public void WithMemoryModeButNoSharedCache_Throws()
        {
            var builder = new StubFlowlyBuilder(new ServiceCollection(), new ConfigurationBuilder().Build());

            Assert.Throws<InvalidOperationException>(() =>
                builder.AddSQLiteMessageStreamPersistence("Data Source=test;Mode=Memory", enableMigrations: false));
        }

        [Fact]
        public void WithSharedMemoryCache_Succeeds()
        {
            var builder = new StubFlowlyBuilder(new ServiceCollection(), new ConfigurationBuilder().Build());

            builder.AddSQLiteMessageStreamPersistence($"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared", enableMigrations: false);

            Assert.NotNull(ResolveConnectionString(builder.Services, null));
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
