using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.Jobs.Services;

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

    public Task StartAsync(CancellationToken _) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken _) => Task.CompletedTask;

    public Task StopAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken _) => Task.CompletedTask;
}