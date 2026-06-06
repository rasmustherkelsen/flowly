namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Represents the details required to register a queue with the message broker. This record is used internally to
///     encapsulate the necessary information for setting up a queue in the Flowly framework. It includes the name of the
///     queue, whether the queue requires sessions, the default time-to-live for messages in the queue, whether messages
///     should be dead-lettered upon expiration, and the duration for which a message is locked during processing. This
///     information is essential for configuring the queue according to the requirements of the message handlers that will
///     be consuming messages from it.
/// </summary>
/// <param name="QueueName">The name of the queue to be registered with the message broker.</param>
/// <param name="RequiresSession">Indicates whether the queue requires session support. Defaults to false.</param>
/// <param name="DefaultMessageTimeToLive">The default time-to-live for messages in the queue. Defaults to null.</param>
/// <param name="DeadLetterOnMessageExpiration">
///     Indicates whether messages should be dead-lettered upon expiration.
///     Defaults to null.
/// </param>
/// <param name="LockDuration">The duration for which a message is locked during processing. Defaults to null.</param>
/// <param name="IsReplyQueue">
///     When <see langword="true" />, the queue is a Flowly reply queue used to return call responses. Topology
///     creators will declare a simple durable queue without DLX, retry queue, or dead-letter infrastructure.
///     Defaults to <see langword="false" />.
/// </param>
public record DeferredQueueRegistration(
    string QueueName,
    bool RequiresSession = false,
    TimeSpan? DefaultMessageTimeToLive = null,
    bool? DeadLetterOnMessageExpiration = null,
    TimeSpan? LockDuration = null,
    bool IsReplyQueue = false);