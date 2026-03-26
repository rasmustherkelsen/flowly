namespace Flowly.MessageInfrastructure.Receivers;

public class HandlerQueueOptions
{
    public string? QueueName { get; set; }

    public TimeSpan? DefaultMessageTimeToLive { get; set; }

    public bool? DeadLetterOnMessageExpiration { get; set; }

    public TimeSpan? LockDuration { get; set; }

    public int? MaxRetries { get; set; }

    public int? RetryDelaySeconds { get; set; }

    public int? MaxConcurrentCalls { get; set; }
}
