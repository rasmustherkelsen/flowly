namespace Flowly;

/// <summary>
///     Attribute to specify whether messages should be dead-lettered upon expiration. This attribute can be applied to
///     message handler classes to define the behavior of the message queue when messages expire. If enabled, messages that
///     reach their time-to-live without being processed will be moved to a dead-letter queue for further analysis or
///     handling, rather than being discarded. This allows for better monitoring and troubleshooting of message processing
///     issues related to message expiration.
/// </summary>
/// <param name="enabled">Is deadlettering enabled for the queue</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DeadLetterOnMessageExpirationAttribute(bool enabled) : Attribute
{
    /// <summary>
    ///     Gets a value indicating whether messages should be dead-lettered upon expiration for the message handler class.
    /// </summary>
    public bool Enabled { get; } = enabled;
}