using Flowly.DeadLetters.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Extension methods for <see cref="IFlowlyBuilder"/> that register SQLite-backed dead letter tracking.
/// </summary>
public static class SQLiteDeadLetterTrackingRegistrationExtension
{
    /// <summary>
    ///     Registers the SQLite-backed dead letter tracking infrastructure with the Flowly builder.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Configures an EF Core data context factory using SQLite, and registers the dead letter repository,
    ///         service, and supporting background services for ingestion health monitoring, automatic cleanup,
    ///         and metrics reporting.
    ///     </para>
    ///     <para>
    ///         This method is idempotent — calling it more than once on the same builder has no effect.
    ///     </para>
    ///     <para>
    ///         After calling this method, opt individual handler or event handler registrations into dead letter
    ///         ingestion by chaining <c>.WithDeadLetterTracking()</c> on them.
    ///     </para>
    /// </remarks>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <param name="connection">
    ///     A connection string name from <c>IConfiguration</c> (resolved under <c>ConnectionStrings:</c>)
    ///     or a literal SQLite connection string, for example
    ///     <c>Data Source=flowly-deadletters.db</c>.
    ///     When a name is provided and found in configuration, the resolved value is used; otherwise the
    ///     parameter value itself is used as the connection string.
    /// </param>
    /// <param name="enableMigrations">
    ///     When <see langword="true"/> (the default), a hosted service is registered that applies any pending EF Core
    ///     migrations for the dead letter data context during application startup. Set to <see langword="false"/> if
    ///     you manage database migrations out-of-band.
    /// </param>
    /// <param name="configure">
    ///     An optional delegate to configure <see cref="DeadLetterTrackingOptions"/>, such as setting
    ///     <see cref="DeadLetterTrackingOptions.DeleteRequeuedMessagesAfter"/> or
    ///     <see cref="DeadLetterTrackingOptions.DeleteDeadLetteredMessagesAfter"/> to enable automatic cleanup of
    ///     old tracking records. When <see langword="null"/>, automatic cleanup is disabled.
    /// </param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> for chaining.</returns>
    public static IFlowlyBuilder AddSQLiteDeadLetterTracking(
        this IFlowlyBuilder flowlyBuilder,
        string connection,
        bool enableMigrations = true,
        Action<DeadLetterTrackingOptions>? configure = null)
    {
        if (configure is not null)
            flowlyBuilder.Services.Configure<DeadLetterTrackingOptions>(configure);

        var connectionString = flowlyBuilder.Configuration.GetConnectionString(connection) ?? connection;

        RegisterInMemoryKeepAliveIfNeeded(flowlyBuilder.Services, connectionString);

        flowlyBuilder.AddDeadLetterTracking(dbOptions =>
        {
            dbOptions.UseSqlite(
                connectionString,
                sqliteOptions =>
                    sqliteOptions.MigrationsAssembly(
                        typeof(SQLiteDeadLetterTrackingRegistrationExtension).Assembly.GetName().Name));
        });

        if (enableMigrations)
            flowlyBuilder.Services.AddDeadLetterDatabaseMigrations();

        return flowlyBuilder;
    }

    private static void RegisterInMemoryKeepAliveIfNeeded(IServiceCollection services, string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (builder.DataSource == ":memory:")
            throw new InvalidOperationException(
                "The ':memory:' data source cannot be used with Flowly SQLite dead letter tracking because it " +
                "creates an isolated database per connection. Use a named shared in-memory database instead: " +
                "Data Source=<name>;Mode=Memory;Cache=Shared");

        if (builder.Mode != SqliteOpenMode.Memory)
            return;

        if (builder.Cache != SqliteCacheMode.Shared)
            throw new InvalidOperationException(
                "SQLite in-memory databases require Cache=Shared when used with Flowly dead letter tracking " +
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
