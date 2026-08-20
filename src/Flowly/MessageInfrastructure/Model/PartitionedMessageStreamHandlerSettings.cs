namespace Flowly.MessageInfrastructure.Model;

internal sealed record PartitionedMessageStreamHandlerSettings<TMessage, THandler>(
    string QueueName,
    string ProviderName,
    string HandlerName,
    string ConsumerName,
    int PartitionCount,
    StartPosition StartPosition,
    int MaxMessagesBeforeProcessing,
    TimeSpan MaxWaitTime,
    int MaxRetries,
    int RetryDelaySeconds);
