using Flowly.Transport;

namespace Flowly.DeadLetters.Repositories;

internal interface IDeadLetterRepository
{
    Task SaveBatch(IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken = default);

    Task SaveBatchForSubscription(IReadOnlyCollection<IDeadLetterMessage> messages, string topicName, string subscriptionName, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastIngestionTime(string queueName, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastIngestionTimeForSubscription(string topicName, string subscriptionName, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IDeadLetter>> GetAll(CancellationToken cancellationToken = default);

    Task<DeadLetterPage> Query(DeadLetterQuery deadLetterQuery, CancellationToken cancellationToken = default);

    Task<IDeadLetter?> Get(string messageId, CancellationToken cancellationToken = default);

    Task<bool> MarkAsRequeued(string messageId, string? requeuedBy, string? replacementMessageBody = null, CancellationToken cancellationToken = default);

    Task RevertRequeued(string messageId, CancellationToken cancellationToken = default);

    Task<bool> DeletePending(string messageId, CancellationToken cancellationToken = default);

    Task<int> DeleteRequeuedOlderThan(TimeSpan age, CancellationToken cancellationToken = default);

    /// <summary>Deletes at most <paramref name="batchSize"/> pending dead letters older than <paramref name="age"/> and returns the ones this call deleted; an empty result means nothing is left to purge.</summary>
    Task<IReadOnlyCollection<PurgedDeadLetter>> DeletePendingOlderThan(TimeSpan age, int batchSize, CancellationToken cancellationToken = default);
}