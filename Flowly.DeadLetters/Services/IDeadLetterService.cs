namespace Flowly.DeadLetters.Services;

public interface IDeadLetterService
{
    Task Requeue(string messageId, string? requeuedBy = null, CancellationToken cancellationToken = default);

    Task Discard(string messageId, CancellationToken cancellationToken = default);
}
