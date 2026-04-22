namespace Flowly.Transport;

/// <summary>
///     Contains constant string values for message properties used by Flowly, such as retry count, target subscription,
///     and scheduled enqueue time. These properties are used to store metadata about messages being processed by Flowly,
///     such as how many times a message has been retried, which subscription it is targeting, and when it is scheduled to
///     be enqueued for processing.
/// </summary>
public static class FlowlyMessageProperties
{
    /// <summary>
    ///     The name of the message property used to store the retry count for a message. This is used by Flowly to track how
    ///     many times a message has been retried. When a message is processed and fails, Flowly can increment this property to
    ///     keep track of how many times the message has been retried. This can be used to implement retry policies, such as
    ///     giving up after a certain number of retries or applying different handling logic based on the retry count.
    /// </summary>
    public const string RetryCount = "flowly-retry-count";

    /// <summary>
    ///     The name of the message property used to store the target subscription for a message. This is used by Flowly to
    ///     determine which subscription a message is targeting. When a message is processed, Flowly can check this property to
    ///     determine which subscription the message is intended for. This can be used to route messages to different handlers
    ///     or to implement different processing logic based on the target subscription.
    /// </summary>
    public const string TargetSubscription = "flowly-target-subscription";

    /// <summary>
    ///     The name of the message property used to store the scheduled enqueue time for a message. This is used by Flowly to
    ///     determine when a message is scheduled to be enqueued for processing. When a message is scheduled to be processed at
    ///     a future time, Flowly can set this property to indicate when the message should be enqueued. This can be used to
    ///     implement delayed processing or to schedule messages for processing at specific times.
    /// </summary>
    public const string ScheduledEnqueueTime = "flowly-scheduled-enqueue-time";
}