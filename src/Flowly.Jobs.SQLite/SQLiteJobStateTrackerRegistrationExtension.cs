using Flowly.Jobs;
using Flowly.Jobs.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Extension methods for registering SQLite-backed job state tracking via <see cref="IFlowlyBuilder"/>.
/// </summary>
public static class SQLiteJobStateTrackerRegistrationExtension
{
    /// <summary>
    ///     Adds job state tracking backed by SQLite.
    /// </summary>
    /// <remarks>
    ///     Registers the EF Core job state repositories using SQLite. By default a hosted service runs pending EF Core
    ///     migrations at startup; set <paramref name="enableMigrations"/> to <see langword="false"/> when
    ///     migrations are managed externally (e.g. via a deployment pipeline).
    /// </remarks>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <param name="connectionString">The SQLite connection string used to connect to the job state database, for example <c>Data Source=flowly-jobs.db</c>.</param>
    /// <param name="enableMigrations">
    ///     When <see langword="true"/> (the default), a hosted service applies pending EF Core migrations
    ///     automatically at application startup. Set to <see langword="false"/> to manage migrations externally.
    /// </param>
    /// <param name="configure">
    ///     An optional delegate to configure <see cref="JobStateTrackingOptions"/>, such as automatic
    ///     deletion intervals for completed and failed jobs.
    /// </param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> instance, for fluent chaining.</returns>
    public static IFlowlyBuilder AddSQLiteJobStateTracking(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString,
        bool enableMigrations = true,
        Action<JobStateTrackingOptions>? configure = null)
    {
        if (configure is not null)
            flowlyBuilder.Services.Configure(configure);

        RegisterInMemoryKeepAliveIfNeeded(flowlyBuilder.Services, connectionString);

        flowlyBuilder
            .AddJobStateTracking()
            .AddRepositories(dbOptions =>
            {
                dbOptions.UseSqlite(
                    connectionString,
                    sqliteOptions =>
                        sqliteOptions.MigrationsAssembly(
                            typeof(SQLiteJobStateTrackerRegistrationExtension).Assembly.GetName().Name));
            });

        if (enableMigrations) flowlyBuilder.Services.AddJobHandlerStateDatabaseMigrations();

        return flowlyBuilder;
    }

    private static void RegisterInMemoryKeepAliveIfNeeded(IServiceCollection services, string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (builder.DataSource == ":memory:")
            throw new InvalidOperationException(
                "The ':memory:' data source cannot be used with Flowly SQLite job state tracking because it " +
                "creates an isolated database per connection. Use a named shared in-memory database instead: " +
                "Data Source=<name>;Mode=Memory;Cache=Shared");

        if (builder.Mode != SqliteOpenMode.Memory)
            return;

        if (builder.Cache != SqliteCacheMode.Shared)
            throw new InvalidOperationException(
                "SQLite in-memory databases require Cache=Shared when used with Flowly job state tracking " +
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

    /// <summary>
    ///     Adds a read-only job state tracking client backed by SQLite.
    /// </summary>
    /// <remarks>
    ///     Registers only the EF Core query repositories — no job processing infrastructure, no background
    ///     services, and no migration service. Use this in applications that need to query job state (e.g.
    ///     an API or dashboard) without participating in job processing.
    /// </remarks>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <param name="connectionString">The SQLite connection string used to connect to the job state database, for example <c>Data Source=flowly-jobs.db</c>.</param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> instance, for fluent chaining.</returns>
    public static IFlowlyBuilder AddJobStateTrackingClient(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString)
    {
        flowlyBuilder.AddRepositories(dbOptions => dbOptions.UseSqlite(connectionString));
        return flowlyBuilder;
    }
}