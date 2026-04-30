using Flowly.Jobs;
using Flowly.Jobs.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Extension methods for registering PostgreSQL-backed job state tracking via <see cref="IFlowlyBuilder"/>.
/// </summary>
public static class PostgresJobStateTrackerRegistrationExtension
{
    /// <summary>
    ///     Adds job state tracking backed by PostgreSQL.
    /// </summary>
    /// <remarks>
    ///     Registers the EF Core job state repositories using Npgsql, with automatic retry on failure
    ///     (up to 5 attempts with a 30-second delay). By default a hosted service runs pending EF Core
    ///     migrations at startup; set <paramref name="enableMigrations"/> to <see langword="false"/> when
    ///     migrations are managed externally (e.g. via a deployment pipeline).
    /// </remarks>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <param name="connectionString">The PostgreSQL connection string used to connect to the job state database.</param>
    /// <param name="enableMigrations">
    ///     When <see langword="true"/> (the default), a hosted service applies pending EF Core migrations
    ///     automatically at application startup. Set to <see langword="false"/> to manage migrations externally.
    /// </param>
    /// <param name="configure">
    ///     An optional delegate to configure <see cref="JobStateTrackingOptions"/>, such as automatic
    ///     deletion intervals for completed and failed jobs.
    /// </param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> instance, for fluent chaining.</returns>
    public static IFlowlyBuilder AddPostgresJobStateTracking(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString,
        bool enableMigrations = true,
        Action<JobStateTrackingOptions>? configure = null)
    {
        if (configure is not null)
            flowlyBuilder.Services.Configure<JobStateTrackingOptions>(configure);

        flowlyBuilder
            .AddJobStateTracking()
            .AddRepositories(dbOptions =>
            {
                dbOptions.UseNpgsql(
                    connectionString,
                    npgsqlOptions =>
                    {
                        npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                        npgsqlOptions.MigrationsAssembly(typeof(PostgresJobStateTrackerRegistrationExtension).Assembly.GetName().Name);
                    });
            });

        if (enableMigrations) flowlyBuilder.Services.AddJobHandlerStateDatabaseMigrations();

        return flowlyBuilder;
    }

    /// <summary>
    ///     Adds a read-only job state tracking client backed by PostgreSQL.
    /// </summary>
    /// <remarks>
    ///     Registers only the EF Core query repositories — no job processing infrastructure, no background
    ///     services, and no migration service. Use this in applications that need to query job state (e.g.
    ///     an API or dashboard) without participating in job processing.
    /// </remarks>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <param name="connectionString">The PostgreSQL connection string used to connect to the job state database.</param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> instance, for fluent chaining.</returns>
    public static IFlowlyBuilder AddJobStateTrackingClient(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString)
    {
        flowlyBuilder.AddRepositories(dbOptions =>
            dbOptions.UseNpgsql(
                connectionString,
                npgsql => npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)));

        return flowlyBuilder;
    }
}