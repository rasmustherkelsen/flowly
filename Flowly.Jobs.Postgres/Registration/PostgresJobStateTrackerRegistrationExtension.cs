using Flowly.Jobs.Registration;
using Flowly.Jobs.Services;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.Postgres.Registration;

public static class PostgresJobStateTrackerRegistrationExtension
{
    /// <summary>
    /// Add job state tracking backed by PostgreSQL.
    /// </summary>
    public static IFlowlyBuilder AddPostgresJobStateTracking(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString,
        bool enableMigrations = true)
    {
        flowlyBuilder
            .AddJobStateTracking()
            .AddRepositories(options =>
            {
                options.UseNpgsql(
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
}
