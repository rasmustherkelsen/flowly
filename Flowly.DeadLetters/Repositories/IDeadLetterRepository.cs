using Flowly.DeadLetters.DatabaseModel;
using Flowly.MessagingAbstractions;

namespace Flowly.DeadLetters.Repositories;

public interface IDeadLetterRepository
{
    Task SaveBatch(IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastIngestionTime(string queueName, CancellationToken cancellationToken = default);

    Task<DeadLetter?> Get(string messageId, CancellationToken cancellationToken = default);

    Task MarkAsRequeued(string messageId, string? requeuedBy, CancellationToken cancellationToken = default);

    Task Delete(string messageId, CancellationToken cancellationToken = default);
}
