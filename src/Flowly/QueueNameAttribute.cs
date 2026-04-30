namespace Flowly;

/// <summary>
///     Attribute to specify the queue name for a message contract. Apply this attribute to a message class to override the
///     default queue name resolution logic. By default, the queue name is typically derived from the message contract's
///     class name. However, if you want to specify a custom queue name for a particular message contract, you can use this
///     attribute to do so. This allows for greater flexibility in organizing and routing messages within the messaging
///     infrastructure, enabling you to group related messages together or to use more descriptive queue names that better
///     reflect the purpose of the messages being processed.
/// </summary>
/// <param name="queueName">Queue name for the message contract</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QueueNameAttribute(string queueName) : Attribute
{
    /// <summary>
    ///     Queue name for the message contract.
    /// </summary>
    public string QueueName { get; } = queueName;
}