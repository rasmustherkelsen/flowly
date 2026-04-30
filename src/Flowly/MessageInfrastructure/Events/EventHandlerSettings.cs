namespace Flowly.MessageInfrastructure.Events;

internal record EventHandlerSettings<TEvent, THandler>(
    string TopicName,
    string SubscriptionName,
    string ProviderName,
    string HandlerName,
    int MaxConcurrentCalls = 1,
    int MaxRetries = 0,
    int RetryDelaySeconds = 0);
