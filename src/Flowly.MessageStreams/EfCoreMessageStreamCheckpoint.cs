using Flowly.MessageStreams.DatabaseModel;
using Microsoft.EntityFrameworkCore;

namespace Flowly.MessageStreams;

/// <summary>
///     A <see cref="MessageStreamCheckpoint{TMessage}" /> backed by one of Flowly's built-in EF Core persistence
///     providers (<c>Flowly.MessageStreams.SqlServer</c>, <c>.Postgres</c>, <c>.SQLite</c>), registered via
///     <see cref="MessageStreamCheckpointRegistrationExtensions.UseMessageStreamCheckpoint{TMessage}" />. A save flagged as a
///     <see cref="MessageStreamCheckpointSaveContext.IsBootstrapSeed">bootstrap seed</see> only writes when the row has no position
///     (a conditional update, or an insert whose unique key conflict is treated as already stored), so a late seed never moves a
///     stored position backwards; any other save overwrites.
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
        var rows = dbContext.Checkpoints.Where(x => x.QueueName == queueName && x.ConsumerName == context.ConsumerName && x.Partition == partition);

        if (context.IsBootstrapSeed)
        {
            await SaveBootstrapSeed(dbContext, rows, context, partition, cancellationToken);
            return;
        }

        var updated = await rows.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(x => x.Position, context.Position)
                .SetProperty(x => x.UpdatedAtUtc, DateTimeOffset.UtcNow),
            cancellationToken);

        if (updated > 0)
            return;

        dbContext.Checkpoints.Add(CreateRecord(context, partition));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveBootstrapSeed(
        MessageStreamCheckpointDataContext dbContext,
        IQueryable<MessageStreamCheckpointRecord> rows,
        MessageStreamCheckpointSaveContext context,
        int partition,
        CancellationToken cancellationToken)
    {
        if (await SetPositionWhenNoneStored(rows, context.Position, cancellationToken) > 0)
            return;

        try
        {
            dbContext.Checkpoints.Add(CreateRecord(context, partition));

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();

            if (!await rows.AnyAsync(cancellationToken))
                throw;

            await SetPositionWhenNoneStored(rows, context.Position, cancellationToken);
        }
    }

    private static Task<int> SetPositionWhenNoneStored(IQueryable<MessageStreamCheckpointRecord> rows, long position, CancellationToken cancellationToken)
        => rows
            .Where(x => x.Position == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Position, position)
                    .SetProperty(x => x.UpdatedAtUtc, DateTimeOffset.UtcNow),
                cancellationToken);

    private MessageStreamCheckpointRecord CreateRecord(MessageStreamCheckpointSaveContext context, int partition) => new()
    {
        QueueName = queueName,
        ConsumerName = context.ConsumerName,
        Partition = partition,
        Position = context.Position,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static int ResolvePartition(int? partition) => partition ?? MessageStreamCheckpointRecord.NonPartitioned;
}
