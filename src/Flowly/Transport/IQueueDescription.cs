namespace Flowly.Transport;

/// <summary>
///     Interface representing the description of a queue in the messaging system, including its name and various
///     configuration settings.
/// </summary>
public interface IQueueDescription
{
    /// <summary>
    ///     Gets the name of the queue. This is the identifier used to send messages to and receive messages from this queue.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets the default time-to-live for messages in this queue. This is the duration after which messages will expire and
    ///     be removed from the queue if they have not been processed.
    /// </summary>
    TimeSpan DefaultMessageTimeToLive { get; }

    /// <summary>
    ///     Gets a value indicating whether messages that expire in this queue should be moved to a dead-letter queue. If true,
    ///     expired messages will be moved to the dead-letter queue for later analysis or reprocessing; if false, expired
    ///     messages will simply be removed from the queue without being retained.
    /// </summary>
    bool DeadLetterOnMessageExpiration { get; }

    /// <summary>
    ///     Gets the duration for which a message is locked when being processed. This is the time during which a message is
    ///     invisible to other handlers while it is being processed by a handler. If a handler fails to process the message
    ///     within this duration, the message will become visible again and can be picked up by another handler for processing.
    /// </summary>
    TimeSpan LockDuration { get; }

    /// <summary>
    ///     Gets a value indicating whether this queue requires session-based processing. If true, messages in this queue will
    ///     be grouped into sessions based on a session identifier, and handlers will process messages from the same session
    ///     sequentially. If false, messages in this queue will be processed independently without session grouping.
    ///     Session-based processing can be useful for scenarios where message order is important or where related messages
    ///     need to be processed together, but it may also introduce additional complexity and overhead in message handling. It
    ///     is important to consider the requirements of your application and the characteristics of your messages when
    ///     deciding whether to use session-based processing for a queue.
    /// </summary>
    bool RequiresSession { get; }
}