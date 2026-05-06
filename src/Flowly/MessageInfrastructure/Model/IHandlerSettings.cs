namespace Flowly.MessageInfrastructure.Model;

/// <summary>
///     Represents the settings for a message handler. This includes information about the queue the handler listens to,
///     the provider it uses, and its retry policies. These settings are used to configure the message processing behavior
///     for a specific handler and message type.
/// </summary>
/// <typeparam name="TMesssage"></typeparam>
internal interface IHandlerSettings<TMesssage>
{
    /// <summary>
    ///     Gets the name of the queue that the handler listens to. This is used to route messages of the specified type to the
    ///     appropriate handler. The queue name is typically derived from the message type, but can be customized through
    ///     configuration.
    /// </summary>
    string QueueName { get; }

    /// <summary>
    ///     Gets the name of the message provider that the handler uses to receive messages. This is used to determine which
    ///     message provider implementation to use for processing messages of the specified type. The provider name is
    ///     typically derived from the message type, but can be customized through configuration.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    ///     Gets the name of the handler. This is used for logging and diagnostics purposes to identify which handler is
    ///     processing messages of the specified type. The handler name is typically derived from the handler class name, but
    ///     can be customized through configuration.
    /// </summary>
    string HandlerName { get; }

    /// <summary>
    ///     Gets a value indicating whether the handler should use "read and delete" semantics when processing messages. If
    ///     true, the handler will read messages from the queue and delete them immediately after processing, without relying
    ///     on message locks or acknowledgments. This can improve performance but may lead to message loss if the handler fails
    ///     during processing. If false, the handler will use "peek-lock" semantics, where messages are locked for processing
    ///     and only deleted after successful completion. This provides better reliability at the cost of potentially increased
    ///     latency and complexity in handling message locks and retries.
    /// </summary>
    bool ReadAndDelete { get; }

    /// <summary>
    ///     Gets the maximum number of concurrent calls that the handler can process. This setting is used to control the level
    ///     of concurrency for message processing. A higher value allows the handler to process more messages in parallel,
    ///     which can improve throughput but may also increase resource usage and contention. A lower value limits the number
    ///     of concurrent messages being processed, which can help reduce resource contention and improve stability, but may
    ///     also decrease throughput. The optimal value for this setting depends on the specific workload, message processing
    ///     time, and available resources of the application.
    /// </summary>
    int MaxConcurrentCalls { get; }

    /// <summary>
    ///     Gets the maximum number of retry attempts for processing a message. If a message processing attempt fails, the
    ///     handler will retry processing the message up to this number of times before giving up and potentially moving
    ///     the message to a dead-letter queue or discarding it, depending on the message provider's behavior. Setting this
    ///     value to a higher number allows for more retry attempts, which can help recover from transient errors, but may also
    ///     lead to longer processing times and increased resource usage if messages consistently fail. Setting this value to
    ///     zero means that no retries will be attempted, and messages will be immediately considered failed upon the first
    ///     processing attempt.
    /// </summary>
    int MaxRetries { get; }

    /// <summary>
    ///     Gets the delay in seconds between retry attempts for processing a message. If a message processing attempt fails
    ///     and the handler is configured to retry, this setting determines how long the handler will wait before attempting to
    ///     process the message again. A longer delay can help reduce the likelihood of repeated failures due to transient
    ///     issues, such as temporary network outages or service unavailability, but may also increase the overall time it
    ///     takes to process messages that experience failures. A shorter delay allows for quicker retries, which can be
    ///     beneficial for recovering from transient errors, but may also lead to more rapid repeated failures if the
    ///     underlying issue is not resolved.
    /// </summary>
    int RetryDelaySeconds { get; }

    /// <summary>
    ///     Gets the number of messages to retrieve from the queue before processing.
    /// </summary>
    int MaxMessagesBeforeProcessing { get; }

    /// <summary>
    ///     Gets the maximum amount of time to wait before processing messages, even if the MaxMessagesBeforeProcessing
    ///     threshold has not been reached.
    /// </summary>
    TimeSpan MaxWaitTime { get; }
}