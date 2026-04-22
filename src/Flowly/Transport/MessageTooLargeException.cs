namespace Flowly.Transport;

/// <summary>
///     Exception thrown when a message exceeds the configured maximum size for a queue.
/// </summary>
public class MessageTooLargeException : Exception
{
    /// <summary>
    ///     Exception thrown when a message exceeds the configured maximum size for a queue.
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="actualSizeBytes"></param>
    /// <param name="maxSizeBytes"></param>
    public MessageTooLargeException(string queueName, long actualSizeBytes, long maxSizeBytes)
        : base($"Message for queue '{queueName}' is {actualSizeBytes:N0} bytes, which exceeds the configured limit of {maxSizeBytes:N0} bytes.")
    {
        QueueName = queueName;
        ActualSizeBytes = actualSizeBytes;
        MaxSizeBytes = maxSizeBytes;
    }

    /// <summary>
    ///     The name of the queue for which the message was too large.
    /// </summary>
    public string QueueName { get; }

    /// <summary>
    ///     The actual size of the message in bytes.
    /// </summary>
    public long ActualSizeBytes { get; }

    /// <summary>
    ///     The configured maximum size for messages on the queue, in bytes.
    /// </summary>
    public long MaxSizeBytes { get; }
}