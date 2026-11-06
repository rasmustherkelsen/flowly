using Flowly.DeadLetters;

namespace Flowly.Dashboard.Tests.Fakes;

internal sealed class RecordingDeadLetterService : IDeadLetterService
{
    public List<DeadLetterQuery> Queries { get; } = [];

    public int GetDeadLettersCalls { get; private set; }

    public DeadLetterPage PageToReturn { get; set; } = new([], null);

    public Task<IReadOnlyCollection<IDeadLetter>> GetDeadLetters(CancellationToken cancellationToken = default)
    {
        GetDeadLettersCalls++;

        return Task.FromResult<IReadOnlyCollection<IDeadLetter>>(PageToReturn.Items);
    }

    public Task<DeadLetterPage> QueryDeadLetters(DeadLetterQuery deadLetterQuery, CancellationToken cancellationToken = default)
    {
        Queries.Add(deadLetterQuery);

        return Task.FromResult(PageToReturn);
    }

    public List<(string MessageId, string? RequeuedBy, string? ReplacementMessageBody)> RequeueCalls { get; } = [];

    public Exception? RequeueFailure { get; set; }

    public Task Requeue(string messageId, string? requeuedBy = null, CancellationToken cancellationToken = default)
        => Requeue(messageId, requeuedBy, null, cancellationToken);

    public Task Requeue(string messageId, string? requeuedBy, string? replacementMessageBody, CancellationToken cancellationToken = default)
    {
        RequeueCalls.Add((messageId, requeuedBy, replacementMessageBody));

        return RequeueFailure is null ? Task.CompletedTask : Task.FromException(RequeueFailure);
    }

    public List<string> DiscardCalls { get; } = [];

    public Exception? DiscardFailure { get; set; }

    public Task Discard(string messageId, CancellationToken cancellationToken = default)
    {
        DiscardCalls.Add(messageId);

        return DiscardFailure is null ? Task.CompletedTask : Task.FromException(DiscardFailure);
    }
}
