using System.Text.Json;
using Flowly.DeadLetters.DatabaseModel;
using Flowly.MessagingAbstractions;
using Microsoft.EntityFrameworkCore;

namespace Flowly.DeadLetters.Repositories;

internal class DeadLetterRepository(IDbContextFactory<DeadLetterDataContext> contextFactory) : IDeadLetterRepository
{
    public async Task SaveBatch(IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken = default)
        => await PersistBatch(messages, queueName, subscriptionName: null, cancellationToken);

    public async Task SaveBatchForSubscription(IReadOnlyCollection<IDeadLetterMessage> messages, string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        => await PersistBatch(messages, topicName, subscriptionName, cancellationToken);

    public async Task<DateTimeOffset?> GetLastIngestionTime(string queueName, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.DeadLetters
            .Where(d => d.QueueName == queueName && d.SubscriptionName == null)
            .MaxAsync(d => (DateTimeOffset?)d.DeadLetteredAt, cancellationToken);
    }

    public async Task<DateTimeOffset?> GetLastIngestionTimeForSubscription(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.DeadLetters
            .Where(d => d.QueueName == topicName && d.SubscriptionName == subscriptionName)
            .MaxAsync(d => (DateTimeOffset?)d.DeadLetteredAt, cancellationToken);
    }

    private async Task PersistBatch(IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, string? subscriptionName, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var incomingIds = messages.Select(m => m.MessageId).ToHashSet();
        var existingIds = await context.DeadLetters
            .Where(d => incomingIds.Contains(d.MessageId))
            .Select(d => d.MessageId)
            .ToHashSetAsync(cancellationToken);

        var newDeadLetters = messages
            .DistinctBy(m => m.MessageId)
            .Where(m => !existingIds.Contains(m.MessageId))
            .Select(message => new DeadLetter
            {
                MessageId = message.MessageId,
                QueueName = queueName,
                SubscriptionName = subscriptionName,
                MessageBody = message.RawBody,
                MessageProperties = JsonSerializer.Serialize(message.ApplicationProperties),
                DeadLetteredAt = message.EnqueuedTime,
                DeadLetterReason = message.DeadLetterReason,
                DeadLetterErrorDescription = message.DeadLetterErrorDescription
            })
            .ToList();

        if (newDeadLetters.Count > 0)
        {
            await context.DeadLetters.AddRangeAsync(newDeadLetters, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<DeadLetter>> GetAll(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.DeadLetters
            .AsNoTracking()
            .OrderByDescending(d => d.DeadLetteredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeadLetter?> Get(string messageId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.DeadLetters
            .FirstOrDefaultAsync(d => d.MessageId == messageId, cancellationToken);
    }

    public async Task MarkAsRequeued(string messageId, string? requeuedBy, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        await context.DeadLetters
            .Where(d => d.MessageId == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DeadLetterStatus.Requeued)
                .SetProperty(d => d.RequeuedAt, DateTimeOffset.UtcNow)
                .SetProperty(d => d.RequeuedBy, requeuedBy),
                cancellationToken);
    }

    public async Task Delete(string messageId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        await context.DeadLetters
            .Where(d => d.MessageId == messageId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteRequeuedOlderThan(TimeSpan age, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var cutOff = DateTimeOffset.UtcNow - age;

        await context.DeadLetters
            .Where(d => d.Status == DeadLetterStatus.Requeued && d.RequeuedAt < cutOff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeletePendingOlderThan(TimeSpan age, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var cutOff = DateTimeOffset.UtcNow - age;

        await context.DeadLetters
            .Where(d => d.Status == DeadLetterStatus.Pending && d.DeadLetteredAt < cutOff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
