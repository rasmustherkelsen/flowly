using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

public static class PostgresDeadLetterTrackingRegistrationExtension
{
    /// <summary>
    /// Add dead letter tracking backed by PostgreSQL.
    /// </summary>
    public static IFlowlyBuilder AddPostgresDeadLetterTracking(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString,
        bool enableMigrations = true,
        Action<DeadLetterTrackingOptions>? configure = null)
    {
        if (configure is not null)
            flowlyBuilder.Services.Configure<DeadLetterTrackingOptions>(configure);

        flowlyBuilder.AddDeadLetterTracking(dbOptions =>
        {
            dbOptions.UseNpgsql(
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
