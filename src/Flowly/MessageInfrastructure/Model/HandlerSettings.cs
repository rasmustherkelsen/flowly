namespace Flowly.MessageInfrastructure.Model;

internal record HandlerSettings<TMessage>(
    string QueueName,
    string ProviderName,
    string HandlerName,
    bool ReadAndDelete,
    int MaxConcurrentCalls,
    int MaxRetries,
    int RetryDelaySeconds,
    int MaxMessagesBeforeProcessing,
    TimeSpan MaxWaitTime) : IHandlerSettings<TMessage>;