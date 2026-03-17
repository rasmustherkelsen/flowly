using Flowly.Jobs.Registration;
using Flowly.Jobs.Services;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.SqlServer.Registration;

public static class SqlServerJobStateTrackerRegistrationExtension
{
    /// <summary>
    /// Add job state tracking backed by SQL Server.
    /// </summary>
    public static IFlowlyBuilder AddSqlServerJobStateTracking(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString,
        bool enableMigrations = true)
    {
        flowlyBuilder
            .AddJobStateTracking()
            .AddRepositories(options =>
            {
                options.UseSqlServer(
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
}
