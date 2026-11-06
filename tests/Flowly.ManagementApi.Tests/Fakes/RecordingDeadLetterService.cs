using Flowly.DeadLetters;

namespace Flowly.ManagementApi.Tests.Fakes;

internal sealed class RecordingDeadLetterService : IDeadLetterService
{
    public List<DeadLetterQuery> Queries { get; } = [];

    public int GetDeadLettersCalls { get; private set; }

    public IReadOnlyList<IDeadLetter> ItemsToReturn { get; set; } = [];

    public Task<IReadOnlyCollection<IDeadLetter>> GetDeadLetters(CancellationToken cancellationToken = default)
    {
        GetDeadLettersCalls++;

        return Task.FromResult<IReadOnlyCollection<IDeadLetter>>(ItemsToReturn);
    }

    public Task<DeadLetterPage> QueryDeadLetters(DeadLetterQuery deadLetterQuery, CancellationToken cancellationToken = default)
    {
        Queries.Add(deadLetterQuery);

        return Task.FromResult(new DeadLetterPage(ItemsToReturn, null));
    }

    public Task Requeue(string messageId, string? requeuedBy = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task Discard(string messageId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
