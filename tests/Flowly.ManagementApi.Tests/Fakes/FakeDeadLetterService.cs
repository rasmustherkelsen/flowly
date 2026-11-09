using Flowly.DeadLetters;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.ManagementApi.Tests.Fakes;

internal sealed class FakeDeadLetterService : IDeadLetterService
{
    private readonly List<FakeDeadLetter> _deadLetters = [];

    public List<(string MessageId, string? RequeuedBy)> RequeueCalls { get; } = [];

    public List<string?> ReplacementBodies { get; } = [];

    public List<string> DiscardCalls { get; } = [];

    public Exception? RequeueFailure { get; set; }

    public FakeDeadLetter Add(
        string messageId,
        string queueName = "orders",
        string? subscriptionName = null,
        DeadLetterStatus status = DeadLetterStatus.Pending,
        DateTimeOffset? deadLetteredAt = null,
        string? reason = null)
    {
        var deadLetter = new FakeDeadLetter
        {
            MessageId = messageId,
            QueueName = queueName,
            SubscriptionName = subscriptionName,
            Status = status,
            DeadLetteredAt = deadLetteredAt ?? new DateTimeOffset(2026, 11, 1, 10, 30, 0, TimeSpan.Zero),
            DeadLetterReason = reason,
        };

        _deadLetters.Add(deadLetter);

        return deadLetter;
    }

    public Task<IReadOnlyCollection<IDeadLetter>> GetDeadLetters(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<IDeadLetter>>(_deadLetters.ToList());

    public Task Requeue(string messageId, string? requeuedBy = null, CancellationToken cancellationToken = default)
    {
        RequeueCalls.Add((messageId, requeuedBy));

        var deadLetter = Find(messageId);

        if (deadLetter.Status != DeadLetterStatus.Pending)
            throw new DeadLetterStatusConflictException($"Dead letter '{messageId}' has status '{deadLetter.Status}' and cannot be requeued.");

        if (RequeueFailure is not null)
            throw RequeueFailure;

        deadLetter.Status = DeadLetterStatus.Requeued;
        deadLetter.RequeuedBy = requeuedBy;
        deadLetter.RequeuedAt = DateTimeOffset.UtcNow;

        return Task.CompletedTask;
    }

    public async Task Requeue(string messageId, string? requeuedBy, string? replacementMessageBody, CancellationToken cancellationToken = default)
    {
        ReplacementBodies.Add(replacementMessageBody);

        if (replacementMessageBody is not null && string.IsNullOrWhiteSpace(replacementMessageBody))
            throw new InvalidReplacementMessageBodyException("The replacement message body must not be empty.");

        await Requeue(messageId, requeuedBy, cancellationToken);

        Find(messageId).ReplacementMessageBody = replacementMessageBody;
    }

    public Task Discard(string messageId, CancellationToken cancellationToken = default)
    {
        DiscardCalls.Add(messageId);

        var deadLetter = Find(messageId);

        if (deadLetter.Status == DeadLetterStatus.Requeued)
            throw new DeadLetterStatusConflictException($"Dead letter '{messageId}' has already been requeued and cannot be discarded.");

        _deadLetters.Remove(deadLetter);

        return Task.CompletedTask;
    }

    public void AddTo(IServiceCollection services) => services.AddScoped<IDeadLetterService>(_ => this);

    private FakeDeadLetter Find(string messageId)
        => _deadLetters.FirstOrDefault(deadLetter => deadLetter.MessageId == messageId)
           ?? throw new KeyNotFoundException($"Dead letter with ID '{messageId}' was not found.");
}
