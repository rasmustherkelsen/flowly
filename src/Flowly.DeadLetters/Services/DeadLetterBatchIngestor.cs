using Flowly.DeadLetters.Repositories;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.Services;

internal class DeadLetterBatchIngestor(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DeadLetterBatchIngestor> logger) : IDeadLetterBatchIngestor
{
    public Task IngestForQueue(IDeadLetterReceiver receiver, IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken)
        => Ingest(receiver, messages, "queue", queueName, repository => repository.SaveBatch(messages, queueName, cancellationToken), cancellationToken);

    public Task IngestForEventSubscription(IDeadLetterReceiver receiver, IReadOnlyCollection<IDeadLetterMessage> messages, string topicName, string subscriptionName, string displayName, CancellationToken cancellationToken)
        => Ingest(
            receiver,
            messages,
            "event subscription",
            displayName,
            repository => repository.SaveBatchForSubscription(messages, topicName, subscriptionName, cancellationToken),
            cancellationToken);

    private async Task Ingest(
        IDeadLetterReceiver receiver,
        IReadOnlyCollection<IDeadLetterMessage> messages,
        string sourceKind,
        string identifier,
        Func<IDeadLetterRepository, Task> saveBatch,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeadLetterRepository>();

        try
        {
            await saveBatch(repository);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist dead letter batch from {SourceKind} '{Identifier}', abandoning {Count} messages", sourceKind, identifier, messages.Count);

            foreach (var message in messages)
            {
                await receiver.AbandonMessage(message, cancellationToken);
            }

            return;
        }

        var completedCount = 0;

        try
        {
            foreach (var message in messages)
            {
                await receiver.CompleteMessage(message, cancellationToken);
                completedCount++;
            }

            logger.LogInformation("Dead letter ingestion persisted {Count} messages from {SourceKind} '{Identifier}'", messages.Count, sourceKind, identifier);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to complete {Count} dead letter messages from {SourceKind} '{Identifier}' after persisting, abandoning remaining messages",
                messages.Count - completedCount,
                sourceKind,
                identifier);

            foreach (var message in messages.Skip(completedCount))
            {
                await receiver.AbandonMessage(message, cancellationToken);
            }
        }
    }
}
