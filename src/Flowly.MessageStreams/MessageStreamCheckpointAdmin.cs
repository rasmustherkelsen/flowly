using Flowly.MessageStreams.DatabaseModel;
using Microsoft.EntityFrameworkCore;

namespace Flowly.MessageStreams;

/// <summary>
///     A stored checkpoint row, as reported by <see cref="MessageStreamCheckpointAdmin.List" />. <see cref="Position" />
///     is <see langword="null" /> for a row that has been initialized but has never had a batch successfully saved.
/// </summary>
internal sealed record MessageStreamCheckpointInfo(string QueueName, string ConsumerName, int? Partition, long? Position, DateTimeOffset UpdatedAtUtc);

/// <summary>
///     Read/write operations against a message stream checkpoint persistence store, shared between the
///     <c>flowly message-stream</c> CLI commands and the Flowly Management API's checkpoint actions so neither
///     duplicates the query/upsert logic.
/// </summary>
internal static class MessageStreamCheckpointAdmin
{
    public static async Task<IReadOnlyList<MessageStreamCheckpointInfo>> List(
        IDbContextFactory<MessageStreamCheckpointDataContext> dbContextFactory,
        string? queueName,
        string? consumerName,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.Checkpoints.AsNoTracking();

        if (queueName is not null)
            query = query.Where(x => x.QueueName == queueName);

        if (consumerName is not null)
            query = query.Where(x => x.ConsumerName == consumerName);

        return await query
            .OrderBy(x => x.QueueName)
            .ThenBy(x => x.ConsumerName)
            .ThenBy(x => x.Partition)
            .Select(x => new MessageStreamCheckpointInfo(
                x.QueueName,
                x.ConsumerName,
                x.Partition == MessageStreamCheckpointRecord.NonPartitioned ? null : x.Partition,
                x.Position,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public static async Task Reset(
        IDbContextFactory<MessageStreamCheckpointDataContext> dbContextFactory,
        string queueName,
        string consumerName,
        int? partition,
        long offset,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var partitionValue = partition ?? MessageStreamCheckpointRecord.NonPartitioned;

        var updated = await context.Checkpoints
            .Where(x => x.QueueName == queueName && x.ConsumerName == consumerName && x.Partition == partitionValue)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Position, offset)
                    .SetProperty(x => x.UpdatedAtUtc, DateTimeOffset.UtcNow),
                cancellationToken);

        if (updated > 0)
            return;

        context.Checkpoints.Add(new MessageStreamCheckpointRecord
        {
            QueueName = queueName,
            ConsumerName = consumerName,
            Partition = partitionValue,
            Position = offset,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
