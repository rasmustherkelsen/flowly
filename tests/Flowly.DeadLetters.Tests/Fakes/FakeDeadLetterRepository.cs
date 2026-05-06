using Flowly.DeadLetters.Repositories;
using Flowly.Transport;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeDeadLetterRepository : IDeadLetterRepository
{
    private readonly Dictionary<string, IDeadLetter> _store = [];

    public string? RequeuedMessageId { get; private set; }
    public string? RequeuedBy { get; private set; }
    public string? DeletedMessageId { get; private set; }

    public Task SaveBatch(IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SaveBatchForSubscription(IReadOnlyCollection<IDeadLetterMessage> messages, string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetLastIngestionTime(string queueName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<DateTimeOffset?>(null);
    }

    public Task<DateTimeOffset?> GetLastIngestionTimeForSubscription(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<DateTimeOffset?>(null);
    }

    public Task<IDeadLetter?> Get(string messageId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.GetValueOrDefault(messageId));
    }

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

    public Task DeleteRequeuedOlderThan(TimeSpan age, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeletePendingOlderThan(TimeSpan age, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<IDeadLetter>> GetAll(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<IDeadLetter>>(_store.Values.ToList());
    }

    public void Add(IDeadLetter deadLetter)
    {
        _store[deadLetter.MessageId] = deadLetter;
    }
}