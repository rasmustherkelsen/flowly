using Flowly.DeadLetters.Registration;
using Flowly.DeadLetters.Services;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.EntityFrameworkCore;

namespace Flowly.DeadLetters.Postgres.Registration;

public static class PostgresDeadLetterTrackingRegistrationExtension
{
    /// <summary>
    /// Add dead letter tracking backed by PostgreSQL.
    /// </summary>
    public static IFlowlyBuilder AddPostgresDeadLetterTracking(this IFlowlyBuilder flowlyBuilder, string connectionString, bool enableMigrations = true)
    {
        flowlyBuilder.AddDeadLetterTracking(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                    npgsqlOptions.MigrationsAssembly(typeof(PostgresDeadLetterTrackingRegistrationExtension).Assembly.GetName().Name);
                });
        });

        if (enableMigrations)
            flowlyBuilder.Services.AddDeadLetterDatabaseMigrations();

        return flowlyBuilder;
    }
}