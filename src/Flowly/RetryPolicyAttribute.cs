namespace Flowly;

/// <summary>
///     Attribute to specify retry policy for message handlers. This attribute can be applied to message handler classes to
///     define the maximum number of retries and the delay between retries when processing messages. The retry policy helps
///     to handle transient failures in message processing by allowing the system to automatically retry failed operations
///     according to the specified parameters.
/// </summary>
/// <param name="maxRetries">The maximum number of retries allowed for failed message processing.</param>
/// <param name="delaySeconds">The delay between retries in seconds.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RetryPolicyAttribute(int maxRetries, int delaySeconds) : Attribute
{
    /// <summary>
    ///     Gets the maximum number of retries allowed for failed message processing.
    /// </summary>
    public int MaxRetries { get; } = maxRetries;

    /// <summary>
    ///     The delay between retries in seconds.
    /// </summary>
    public int DelaySeconds { get; } = delaySeconds;
}