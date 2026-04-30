namespace Flowly.MessageInfrastructure.Receivers;

internal sealed record ResolvedMessageHandlerOptions(
    string QueueName,
    TimeSpan DefaultMessageTimeToLive,
    bool DeadLetterOnMessageExpiration,
    TimeSpan LockDuration,
    int MaxRetries,
    int RetryDelaySeconds,
    int MaxConcurrentCalls,
    int MaxMessagesBeforeProcessing,
    TimeSpan MaxWaitTime);
