namespace Flowly.DeadLetters.Services;

internal interface IDeadLetterIngestionHealthChecker
{
    Task CheckAll(CancellationToken cancellationToken);
}
