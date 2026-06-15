using Flowly.DeadLetters.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Extension methods on <see cref="IFlowlyBuilder"/> that wire up SQL Server as the persistence backend
///     for dead letter tracking.
/// </summary>
public static class SqlServerDeadLetterTrackingRegistrationExtension
{
    /// <summary>
    ///     Adds dead letter tracking backed by SQL Server. Registers the EF Core data context, repository,
    ///     service, and supporting background services needed to ingest and store dead-lettered messages.
    ///     Retry-on-failure is configured automatically (up to 5 retries with a 30-second delay).
    ///     <para>
    ///         After calling this method, opt individual handlers into dead letter ingestion by chaining
    ///         <c>.WithDeadLetterTracking()</c> on each <c>AddMessageHandler</c> or <c>AddEventHandler</c>
    ///         registration.
    ///     </para>
    /// </summary>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <param name="connection">
    ///     A connection string name from <c>IConfiguration</c> (resolved under <c>ConnectionStrings:</c>)
    ///     or a literal SQL Server connection string
    ///     (e.g. <c>"Server=localhost;Database=Flowly;..."</c>).
    ///     When a name is provided and found in configuration, the resolved value is used; otherwise the
    ///     parameter value itself is used as the connection string.
    /// </param>
    /// <param name="enableMigrations">
    ///     When <see langword="true"/> (the default), a hosted service runs EF Core migrations automatically
    ///     at application startup, creating or upgrading the <c>DeadLetters</c> table as needed.
    ///     Set to <see langword="false"/> when you manage migrations out-of-band
    ///     (for example, via a deployment pipeline or a dedicated migration tool).
    /// </param>
    /// <param name="configure">
    ///     An optional delegate to configure <see cref="DeadLetterTrackingOptions"/>, such as setting
    ///     <see cref="DeadLetterTrackingOptions.DeleteRequeuedMessagesAfter"/> or
    ///     <see cref="DeadLetterTrackingOptions.DeleteDeadLetteredMessagesAfter"/> to enable automatic
    ///     cleanup of old tracking records.
    /// </param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> instance so that calls can be chained.</returns>
    public static IFlowlyBuilder AddSqlServerDeadLetterTracking(
        this IFlowlyBuilder flowlyBuilder,
        string connection,
        bool enableMigrations = true,
        Action<DeadLetterTrackingOptions>? configure = null)
    {
        if (configure is not null)
            flowlyBuilder.Services.Configure(configure);

        var connectionString = flowlyBuilder.Configuration.GetConnectionString(connection) ?? connection;

        flowlyBuilder.AddDeadLetterTracking(dbOptions =>
        {
            dbOptions.UseSqlServer(
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