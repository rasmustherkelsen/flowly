using Flowly.Jobs.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

public static class SqlServerJobStateTrackerRegistrationExtension
{
    /// <summary>
    /// Add job state tracking backed by SQL Server.
    /// </summary>
    public static IFlowlyBuilder AddSqlServerJobStateTracking(
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
                dbOptions.UseSqlServer(
                    connectionString,
                    sqlServerOptions =>
                    {
                        sqlServerOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                        sqlServerOptions.MigrationsAssembly(typeof(SqlServerJobStateTrackerRegistrationExtension).Assembly.GetName().Name);
                    });
            });

        if (enableMigrations)
        {
            flowlyBuilder.Services.AddJobHandlerStateDatabaseMigrations();
        }

        return flowlyBuilder;
    }

    /// <summary>
    /// Add read-only job tracking client backed by SQL Server.
    /// Use this in applications that need to query job state but do not process jobs.
    /// </summary>
    public static IFlowlyBuilder AddJobStateTrackingClient(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString)
    {
        flowlyBuilder.AddRepositories(dbOptions =>
            dbOptions.UseSqlServer(
                connectionString,
                sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)));

        return flowlyBuilder;
    }
}
