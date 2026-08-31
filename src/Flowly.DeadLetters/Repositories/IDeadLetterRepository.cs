using Flowly.Transport;

namespace Flowly.DeadLetters.Repositories;

internal interface IDeadLetterRepository
{
    Task SaveBatch(IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken = default);

    Task SaveBatchForSubscription(IReadOnlyCollection<IDeadLetterMessage> messages, string topicName, string subscriptionName, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastIngestionTime(string queueName, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastIngestionTimeForSubscription(string topicName, string subscriptionName, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IDeadLetter>> GetAll(CancellationToken cancellationToken = default);

    Task<IDeadLetter?> Get(string messageId, CancellationToken cancellationToken = default);

    Task MarkAsRequeued(string messageId, string? requeuedBy, CancellationToken cancellationToken = default);

    Task Delete(string messageId, CancellationToken cancellationToken = default);

    Task<int> DeleteRequeuedOlderThan(TimeSpan age, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PurgedDeadLetter>> DeletePendingOlderThan(TimeSpan age, CancellationToken cancellationToken = default);
}