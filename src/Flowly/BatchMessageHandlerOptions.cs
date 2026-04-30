namespace Flowly;

/// <summary>
///     Configuration options for batch message handlers. Inherits all queue configuration from
///     <see cref="HandlerQueueOptions" /> and extends it with batch-specific settings.
///     Pass an instance of this class to <see cref="BatchMessageHandler{TMessage}.Configure" /> to configure both
///     queue-level and batch-level behaviour in a single method.
/// </summary>
public class BatchMessageHandlerOptions : HandlerQueueOptions
{
    /// <summary>
    ///     The number of messages to retrieve from the queue before processing. If not set, the handler will process messages
    ///     one by one as they arrive. Setting this option allows the handler to process messages in batches, which can improve
    ///     performance by reducing the number of individual message processing operations. However, it also means that
    ///     messages will wait in the queue until the specified number of messages is reached before being processed, which can
    ///     increase latency for individual messages.
    /// </summary>
    public int? MaxMessagesBeforeProcessing { get; set; }

    /// <summary>
    ///     The maximum amount of time to wait before processing messages, even if the MaxMessagesBeforeProcessing threshold
    ///     has not been reached. This option ensures that messages are not left waiting indefinitely in the queue if the batch
    ///     size is not met. If not set, the handler will wait indefinitely until the MaxMessagesBeforeProcessing threshold is
    ///     reached before processing messages. Setting this option allows for a balance between batch processing and timely
    ///     message handling, ensuring that messages are processed within a reasonable timeframe even if the batch size is not
    ///     met. If message time to live is lower than the max wait time, messages might be evicted from the queue before they
    ///     are processed, so it is important to consider the interaction between these settings when configuring the handler.
    /// </summary>
    public TimeSpan? MaxWaitTime { get; set; }
}