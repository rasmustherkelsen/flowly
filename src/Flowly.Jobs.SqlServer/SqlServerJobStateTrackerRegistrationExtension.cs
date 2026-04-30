using Flowly.Jobs;
using Flowly.Jobs.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering SQL Server-based job state tracking in the Flowly framework. This allows
///     applications to persist and query the lifecycle of jobs (e.g. when they were created, started, completed, or
///     failed) using a SQL Server database. The main extension method, <c>AddSqlServerJobStateTracking</c>, configures the
///     necessary services and repositories for job state tracking with SQL Server as the backend. An additional method,
///     <c>AddJobStateTrackingClient</c>, is provided for applications that only need read-only access to job state
///     information without processing jobs. Both methods support connection resiliency and optional automatic application
///     of EF Core migrations for the job state tracking database schema.
/// </summary>
public static class SqlServerJobStateTrackerRegistrationExtension
{
    /// <summary>
    ///     Add job state tracking backed by SQL Server.
    /// </summary>
    public static IFlowlyBuilder AddSqlServerJobStateTracking(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString,
        bool enableMigrations = true,
        Action<JobStateTrackingOptions>? configure = null)
    {
        if (configure is not null)
            flowlyBuilder.Services.Configure(configure);

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

        if (enableMigrations) flowlyBuilder.Services.AddJobHandlerStateDatabaseMigrations();

        return flowlyBuilder;
    }

    /// <summary>
    ///     Add a read-only job tracking client backed by SQL Server.
    ///     Use this in applications that need to query job state but do not process jobs.
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