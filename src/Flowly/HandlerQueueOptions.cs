namespace Flowly;

/// <summary>
///     Options for configuring the handler queue, such as queue name, message time to live, retry policies, etc.
/// </summary>
public class HandlerQueueOptions
{
    /// <summary>
    ///     The name of the underlying queue. If not set, default convention based naming will be used.
    /// </summary>
    public string? QueueName { get; set; }

    /// <summary>
    ///     The default time to live for messages in the queue. If not set, default options will be applied.
    /// </summary>
    public TimeSpan? DefaultMessageTimeToLive { get; set; }

    /// <summary>
    ///     Whether messages should be dead-lettered when they expire. If not set, default options will be applied.
    /// </summary>
    public bool? DeadLetterOnMessageExpiration { get; set; }

    /// <summary>
    ///     The duration for which a message is locked when being processed. If not set, default options will be applied.
    ///     Setting this option allows you to control how long a message is locked while being processed, which can help
    ///     prevent multiple handlers from processing the same message simultaneously. However, setting this option too low may
    ///     result in messages being unlocked and processed by another handler before the first handler has finished
    ///     processing, while setting it too high may result in messages being locked for longer than necessary if a handler
    ///     fails or takes a long time to process a message. It is important to consider the expected processing time for
    ///     messages and the potential for handler failures when configuring this option.
    /// </summary>
    public TimeSpan? LockDuration { get; set; }

    /// <summary>
    ///     The maximum number of retry attempts for processing a message. Default is to not retry a message. If dead lettering
    ///     is enabled, this will move it to the DLQ at first failure.
    /// </summary>
    public int? MaxRetries { get; set; }

    /// <summary>
    ///     The delay in seconds between retry attempts when processing a message fails. Default is not delay.
    /// </summary>
    public int? RetryDelaySeconds { get; set; }

    /// <summary>
    ///     How many instances of the message handler can be running concurrently.  Default is 1. Setting this option allows
    ///     you to control the level of concurrency for message processing, which can help improve throughput and reduce
    ///     latency. However, setting this option too high may result in resource contention and increased load on the message
    ///     broker, while setting it too low may result in underutilization of resources and slower message processing. It is
    ///     important to consider the expected message volume, processing time, and available resources when configuring this
    ///     option. This will only fan out in the current application instance. Consider application fanout for large volumes.
    /// </summary>
    public int? MaxConcurrentCalls { get; set; }
}