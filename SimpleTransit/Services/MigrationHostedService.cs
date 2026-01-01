using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SimpleTransit.Services;

[ExcludeFromCodeCoverage]
internal class MigrationHostedService<TDbContext>(IServiceScopeFactory scopeFactory, ILogger<MigrationHostedService<TDbContext>> logger) : IHostedLifecycleService where TDbContext : DbContext
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying migrations for {DbContext}", typeof(TDbContext).Name);

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}