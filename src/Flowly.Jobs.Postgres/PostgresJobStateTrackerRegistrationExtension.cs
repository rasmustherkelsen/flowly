using Flowly.Jobs.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

public static class PostgresJobStateTrackerRegistrationExtension
{
    /// <summary>
    /// Add job state tracking backed by PostgreSQL.
    /// </summary>
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

        if (enableMigrations)
        {
            flowlyBuilder.Services.AddJobHandlerStateDatabaseMigrations();
        }

        return flowlyBuilder;
    }

    /// <summary>
    /// Add read-only job tracking client backed by PostgreSQL.
    /// Use this in applications that need to query job state but do not process jobs.
    /// </summary>
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
