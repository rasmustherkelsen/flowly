using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Repositories;
using Flowly.MessagingAbstractions;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeDeadLetterRepository : IDeadLetterRepository
{
    private readonly Dictionary<string, DeadLetter> _store = [];

    public string? RequeuedMessageId { get; private set; }
    public string? RequeuedBy { get; private set; }
    public string? DeletedMessageId { get; private set; }

    public void Add(DeadLetter deadLetter) => _store[deadLetter.MessageId] = deadLetter;

    public Task SaveBatch(IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveBatchForSubscription(IReadOnlyCollection<IDeadLetterMessage> messages, string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<DateTimeOffset?> GetLastIngestionTime(string queueName, CancellationToken cancellationToken = default)
        => Task.FromResult<DateTimeOffset?>(null);

    public Task<DateTimeOffset?> GetLastIngestionTimeForSubscription(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        => Task.FromResult<DateTimeOffset?>(null);

    public Task<DeadLetter?> Get(string messageId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.GetValueOrDefault(messageId));

    public Task MarkAsRequeued(string messageId, string? requeuedBy, CancellationToken cancellationToken = default)
    {
        RequeuedMessageId = messageId;
        RequeuedBy = requeuedBy;
        return Task.CompletedTask;
    }

    public Task Delete(string messageId, CancellationToken cancellationToken = default)
    {
        DeletedMessageId = messageId;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DeadLetter>> GetAll(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DeadLetter>>(_store.Values.ToList());

    public Task DeleteRequeuedOlderThan(TimeSpan age, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeletePendingOlderThan(TimeSpan age, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
