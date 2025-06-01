using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace SimpleTransit.Services;

[ExcludeFromCodeCoverage]
internal class MigrationHostedService<TDbContext>(IServiceScopeFactory scopeFactory, ILogger<MigrationHostedService<TDbContext>> logger) : IHostedService where TDbContext : DbContext
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying migrations for {DbContext}", typeof(TDbContext).Name);

        await using var scope = scopeFactory.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}