using Flowly.DeadLetters.DatabaseModel;

namespace Flowly.DeadLetters.Services;

public interface IDeadLetterService
{
    Task<IReadOnlyCollection<DeadLetter>> GetDeadLetters(CancellationToken cancellationToken = default);

    Task Requeue(string messageId, string? requeuedBy = null, CancellationToken cancellationToken = default);

    Task Discard(string messageId, CancellationToken cancellationToken = default);
}
