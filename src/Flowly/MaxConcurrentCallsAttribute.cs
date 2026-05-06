namespace Flowly;

/// <summary>
///     Specifies the maximum number of concurrent calls to the message handler. This can be used to control the level of
///     concurrency when processing messages from a queue or topic subscription. By setting this attribute on a message
///     handler class, you can limit the number of messages that are processed simultaneously, which can help to manage
///     resource utilization and ensure that the message handler does not become overwhelmed with too many concurrent
///     messages. The Flowly framework will use this attribute to configure the message processing pipeline accordingly,
///     allowing for efficient and controlled message handling based on the specified concurrency settings.
/// </summary>
/// <param name="maxConcurrentCalls">Maximum number of concurrent calls to the message handler in this application instance</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MaxConcurrentCallsAttribute(int maxConcurrentCalls) : Attribute
{
    /// <summary>
    ///     Maximum number of concurrent calls to the message handler in this application instance.
    /// </summary>
    public int MaxConcurrentCalls { get; } = maxConcurrentCalls;
}