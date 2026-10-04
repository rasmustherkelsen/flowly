using Flowly.MessageStreams;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering SQLite-backed message stream checkpoint persistence.
/// </summary>
public static class SQLiteMessageStreamPersistenceRegistrationExtension
{
    /// <summary>
    ///     Registers a message stream checkpoint persistence store backed by SQLite. The first unnamed
    ///     registration becomes the default store used by <c>UseMessageStreamCheckpoint&lt;TMessage&gt;()</c> when no
    ///     name is given; a second unnamed registration throws <see cref="InvalidOperationException" />.
    /// </summary>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <param name="connection">
    ///     The name of a connection string in <c>IConfiguration</c> (looked up under <c>ConnectionStrings:</c>),
    ///     or a literal SQLite connection string if no matching entry is found, for example
    ///     <c>Data Source=flowly-message-streams.db</c>.
    /// </param>
    /// <param name="name">
    ///     An optional name distinguishing this store from other registered message stream persistence stores.
    ///     Omit to register it as the default store.
    /// </param>
    /// <param name="enableMigrations">
    ///     When <see langword="true"/> (the default), a hosted service applies pending EF Core migrations
    ///     automatically at application startup. Set to <see langword="false"/> to manage migrations externally.
    /// </param>
    /// <param name="configure">
    ///     An optional delegate to configure <see cref="MessageStreamCheckpointPersistenceOptions"/>.
    /// </param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> instance, for fluent chaining.</returns>
    public static IFlowlyBuilder AddSQLiteMessageStreamPersistence(
        this IFlowlyBuilder flowlyBuilder,
        string connection,
        string? name = null,
        bool enableMigrations = true,
        Action<MessageStreamCheckpointPersistenceOptions>? configure = null)
    {
        var connectionString = flowlyBuilder.Configuration.GetConnectionString(connection) ?? connection;

        RegisterInMemoryKeepAliveIfNeeded(flowlyBuilder.Services, connectionString);

        return flowlyBuilder.AddMessageStreamCheckpointPersistence(
            name,
            dbOptions => dbOptions.UseSqlite(
                connectionString,
                sqliteOptions =>
                    sqliteOptions.MigrationsAssembly(
                        typeof(SQLiteMessageStreamPersistenceRegistrationExtension).Assembly.GetName().Name)),
            enableMigrations,
            configure);
    }

    private static void RegisterInMemoryKeepAliveIfNeeded(IServiceCollection services, string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (builder.DataSource == ":memory:")
            throw new InvalidOperationException(
                "The ':memory:' data source cannot be used with Flowly SQLite message stream persistence because it " +
                "creates an isolated database per connection. Use a named shared in-memory database instead: " +
                "Data Source=<name>;Mode=Memory;Cache=Shared");

        if (builder.Mode != SqliteOpenMode.Memory)
            return;

        if (builder.Cache != SqliteCacheMode.Shared)
            throw new InvalidOperationException(
                "SQLite in-memory databases require Cache=Shared when used with Flowly message stream persistence " +
                "because the infrastructure opens multiple connections. " +
                "Use: Data Source=<name>;Mode=Memory;Cache=Shared");

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        services.AddSingleton(new InMemoryConnectionKeepAlive(connection));
    }

    private sealed class InMemoryConnectionKeepAlive(SqliteConnection connection) : IDisposable
    {
        public void Dispose() => connection.Dispose();
    }
}
