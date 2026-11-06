using Flowly.DeadLetters.Repositories;
using Flowly.Transport;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeDeadLetterRepository : IDeadLetterRepository
{
    private readonly Dictionary<string, IDeadLetter> _store = [];

    public string? RequeuedMessageId { get; private set; }
    public string? RequeuedBy { get; private set; }
    public string? RequeuedReplacementMessageBody { get; private set; }
    public string? RevertedMessageId { get; private set; }
    public bool MarkAsRequeuedResult { get; set; } = true;
    public bool DeletePendingResult { get; set; } = true;
    public string? DeletedMessageId { get; private set; }
    public int RequeuedOlderThanCountToReturn { get; set; }
    public IReadOnlyCollection<PurgedDeadLetter> PendingOlderThanToReturn { get; set; } = [];
    public List<int> PendingOlderThanBatchSizes { get; } = [];
    private bool _pendingOlderThanReturned;
    public bool ThrowOnSaveBatch { get; set; }
    public string? MessageIdToThrowOnDelete { get; set; }
    public List<IDeadLetterMessage> SavedMessages { get; } = [];
    public string? SavedQueueName { get; private set; }
    public string? SavedSubscriptionName { get; private set; }

    public Task SaveBatch(IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSaveBatch)
            throw new InvalidOperationException("Simulated save failure");

        SavedMessages.AddRange(messages);
        SavedQueueName = queueName;
        return Task.CompletedTask;
    }

    public Task SaveBatchForSubscription(IReadOnlyCollection<IDeadLetterMessage> messages, string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSaveBatch)
            throw new InvalidOperationException("Simulated save failure");

        SavedMessages.AddRange(messages);
        SavedQueueName = topicName;
        SavedSubscriptionName = subscriptionName;
        return Task.CompletedTask;
    }

    public DateTimeOffset? LastIngestionTimeToReturn { get; set; }
    public DateTimeOffset? LastIngestionTimeForSubscriptionToReturn { get; set; }

    public Task<DateTimeOffset?> GetLastIngestionTime(string queueName, CancellationToken cancellationToken = default)
        => Task.FromResult(LastIngestionTimeToReturn);

    public Task<DateTimeOffset?> GetLastIngestionTimeForSubscription(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        => Task.FromResult(LastIngestionTimeForSubscriptionToReturn);

    public Task<IDeadLetter?> Get(string messageId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.GetValueOrDefault(messageId));

    public Task<bool> MarkAsRequeued(string messageId, string? requeuedBy, string? replacementMessageBody = null, CancellationToken cancellationToken = default)
    {
        if (!MarkAsRequeuedResult)
            return Task.FromResult(false);

        RequeuedMessageId = messageId;
        RequeuedBy = requeuedBy;
        RequeuedReplacementMessageBody = replacementMessageBody;
        return Task.FromResult(true);
    }

    public Task RevertRequeued(string messageId, CancellationToken cancellationToken = default)
    {
        RevertedMessageId = messageId;
        return Task.CompletedTask;
    }

    public Task<bool> DeletePending(string messageId, CancellationToken cancellationToken = default)
    {
        if (messageId == MessageIdToThrowOnDelete)
            throw new InvalidOperationException("Simulated delete failure");

        if (!DeletePendingResult)
            return Task.FromResult(false);

        DeletedMessageId = messageId;
        return Task.FromResult(true);
    }

    public Task<int> DeleteRequeuedOlderThan(TimeSpan age, CancellationToken cancellationToken = default)
        => Task.FromResult(RequeuedOlderThanCountToReturn);

    public Task<IReadOnlyCollection<PurgedDeadLetter>> DeletePendingOlderThan(TimeSpan age, int batchSize, CancellationToken cancellationToken = default)
    {
        PendingOlderThanBatchSizes.Add(batchSize);

        if (_pendingOlderThanReturned)
            return Task.FromResult<IReadOnlyCollection<PurgedDeadLetter>>([]);

        _pendingOlderThanReturned = true;
        return Task.FromResult(PendingOlderThanToReturn);
    }

    public Task<IReadOnlyCollection<IDeadLetter>> GetAll(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<IDeadLetter>>(_store.Values.ToList());

    public DeadLetterQuery? QueriedWith { get; private set; }
    public DeadLetterPage PageToReturn { get; set; } = new([], null);

    public Task<DeadLetterPage> Query(DeadLetterQuery deadLetterQuery, CancellationToken cancellationToken = default)
    {
        QueriedWith = deadLetterQuery;
        return Task.FromResult(PageToReturn);
    }

    public void Add(IDeadLetter deadLetter) => _store[deadLetter.MessageId] = deadLetter;
}