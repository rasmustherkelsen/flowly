using Flowly.DeadLetters.Registration;
using Flowly.DeadLetters.Services;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.EntityFrameworkCore;

namespace Flowly.DeadLetters.SqlServer.Registration;

public static class SqlServerDeadLetterTrackingRegistrationExtension
{
    /// <summary>
    /// Add dead letter tracking backed by SQL Server.
    /// </summary>
    public static IFlowlyBuilder AddSqlServerDeadLetterTracking(this IFlowlyBuilder flowlyBuilder, string connectionString, bool enableMigrations = true)
    {
        flowlyBuilder.AddDeadLetterTracking(options =>
        {
            options.UseSqlServer(
                connectionString,
                sqlServerOptions =>
                {
                    sqlServerOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                    sqlServerOptions.MigrationsAssembly(typeof(SqlServerDeadLetterTrackingRegistrationExtension).Assembly.GetName().Name);
                });
        });

        if (enableMigrations)
            flowlyBuilder.Services.AddDeadLetterDatabaseMigrations();

        return flowlyBuilder;
    }
}