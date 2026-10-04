using Flowly.Transport;

namespace Flowly.DeadLetters.Services;

internal interface IDeadLetterBatchIngestor
{
    Task IngestForQueue(IDeadLetterReceiver receiver, IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken);

    Task IngestForEventSubscription(IDeadLetterReceiver receiver, IReadOnlyCollection<IDeadLetterMessage> messages, string topicName, string subscriptionName, string displayName, CancellationToken cancellationToken);
}
