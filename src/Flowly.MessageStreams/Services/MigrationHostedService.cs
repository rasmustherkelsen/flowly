using System.Diagnostics.CodeAnalysis;
using Flowly.MessageStreams.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageStreams.Services;

/// <summary>
///     Applies pending EF Core migrations for one named message stream checkpoint persistence store at startup.
///     A dedicated instance is registered per store (via a factory-based <c>AddSingleton&lt;IHostedService&gt;</c>
///     call capturing that store's specific <see cref="IDbContextFactory{TContext}" />) because, unlike
///     <c>Flowly.Jobs</c>/<c>Flowly.DeadLetters</c>, message stream checkpoint persistence supports multiple named
///     stores in the same process — there is no single ambient <c>MessageStreamCheckpointDataContext</c> registration
///     to resolve from a DI scope the way the single-provider Jobs/DeadLetters migration service does.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class MigrationHostedService(
    IDbContextFactory<MessageStreamCheckpointDataContext> dbContextFactory,
    ILogger<MigrationHostedService> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying migrations for MessageStreamCheckpointDataContext");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StartAsync(CancellationToken _) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken _) => Task.CompletedTask;

    public Task StopAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken _) => Task.CompletedTask;
}
