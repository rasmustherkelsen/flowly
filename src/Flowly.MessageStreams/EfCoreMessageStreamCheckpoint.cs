using Flowly.MessageStreams.DatabaseModel;
using Microsoft.EntityFrameworkCore;

namespace Flowly.MessageStreams;

/// <summary>
///     A <see cref="MessageStreamCheckpoint{TMessage}" /> backed by one of Flowly's built-in EF Core persistence
///     providers (<c>Flowly.MessageStreams.SqlServer</c>, <c>.Postgres</c>, <c>.SQLite</c>), registered via
///     <see cref="MessageStreamCheckpointRegistrationExtensions.UseMessageStreamCheckpoint{TMessage}" />.
/// </summary>
/// <typeparam name="TMessage">The message contract type this checkpoint tracks a position for.</typeparam>
internal class EfCoreMessageStreamCheckpoint<TMessage>(IDbContextFactory<MessageStreamCheckpointDataContext> dbContextFactory, string queueName)
    : MessageStreamCheckpoint<TMessage>
{
    protected override async Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await dbContext.Checkpoints
            .AsNoTracking()
            .AnyAsync(x => x.QueueName == queueName && x.ConsumerName == context.ConsumerName && x.Partition == ResolvePartition(context.Partition), cancellationToken);

        if (exists)
            return;

        try
        {
            dbContext.Checkpoints.Add(new MessageStreamCheckpointRecord
            {
                QueueName = queueName,
                ConsumerName = context.ConsumerName,
                Partition = ResolvePartition(context.Partition),
                Position = null,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent InitializeCheckpoint call (e.g. another partition's runner) already created the row.
        }
    }

    protected override async Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Checkpoints
            .AsNoTracking()
            .Where(x => x.QueueName == queueName && x.ConsumerName == context.ConsumerName && x.Partition == ResolvePartition(context.Partition))
            .Select(x => x.Position)
            .FirstOrDefaultAsync(cancellationToken);
    }

    protected override async Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var partition = ResolvePartition(context.Partition);

        var updated = await dbContext.Checkpoints
            .Where(x => x.QueueName == queueName && x.ConsumerName == context.ConsumerName && x.Partition == partition)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Position, context.Position)
                    .SetProperty(x => x.UpdatedAtUtc, DateTimeOffset.UtcNow),
                cancellationToken);

        if (updated > 0)
            return;

        dbContext.Checkpoints.Add(new MessageStreamCheckpointRecord
        {
            QueueName = queueName,
            ConsumerName = context.ConsumerName,
            Partition = partition,
            Position = context.Position,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static int ResolvePartition(int? partition) => partition ?? MessageStreamCheckpointRecord.NonPartitioned;
}
