namespace Flowly.MessageInfrastructure.Receivers;

public sealed record ResolvedHandlerQueueOptions(
    string QueueName,
    TimeSpan DefaultMessageTimeToLive,
    bool DeadLetterOnMessageExpiration,
    TimeSpan LockDuration,
    int MaxRetries = 0,
    int RetryDelaySeconds = 0,
    int MaxConcurrentCalls = 1);