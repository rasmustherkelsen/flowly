namespace Flowly.MessageInfrastructure.Model;

internal sealed record MessageStreamHandlerSettings<TMessage, THandler>(
    string QueueName,
    string ProviderName,
    string HandlerName,
    string ConsumerName,
    StartPosition StartPosition,
    int MaxMessagesBeforeProcessing,
    TimeSpan MaxWaitTime,
    int MaxRetries,
    int RetryDelaySeconds);
