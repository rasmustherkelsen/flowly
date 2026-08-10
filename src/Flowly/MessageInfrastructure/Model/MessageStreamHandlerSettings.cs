namespace Flowly.MessageInfrastructure.Model;

internal sealed record MessageStreamHandlerSettings<TMessage>(
    string QueueName,
    string ProviderName,
    string HandlerName,
    StartPosition StartPosition,
    int MaxMessagesBeforeProcessing,
    TimeSpan MaxWaitTime,
    int MaxRetries,
    int RetryDelaySeconds);
