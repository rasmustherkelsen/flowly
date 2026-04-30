using System.Diagnostics.CodeAnalysis;
using Flowly.DeadLetters.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.Services;

[ExcludeFromCodeCoverage]
internal class MigrationHostedService(IServiceScopeFactory scopeFactory, ILogger<MigrationHostedService> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying migrations for DeadLetterDataContext");

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DeadLetterDataContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StartAsync(CancellationToken _) => Task.CompletedTask;
    public Task StartedAsync(CancellationToken _) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken _) => Task.CompletedTask;
    public Task StopAsync(CancellationToken _) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken _) => Task.CompletedTask;
}

/// <summary>
///     Extension methods for <see cref="IServiceCollection"/> that register EF Core migration execution for the
///     dead letter tracking data context.
/// </summary>
[ExcludeFromCodeCoverage]
public static class DeadLetterMigrationRegistration
{
    /// <summary>
    ///     Registers a hosted service that applies any pending EF Core migrations for the dead letter data context
    ///     during application startup. Called automatically by the provider-specific registration methods
    ///     (<c>AddSqlServerDeadLetterTracking</c> / <c>AddPostgresDeadLetterTracking</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the migration service to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeadLetterDatabaseMigrations(this IServiceCollection services)
    {
        services.AddHostedService<MigrationHostedService>();
        return services;
    }
}
