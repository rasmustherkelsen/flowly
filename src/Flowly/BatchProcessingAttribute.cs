namespace Flowly;

/// <summary>
///     Attribute to specify batch processing settings for message handlers. This attribute can be applied to message
///     handler classes to define the maximum number of messages to process in a batch and the maximum wait time before
///     processing the batch. Batch processing can improve performance by allowing multiple messages to be processed
///     together, reducing overhead and increasing throughput. The settings defined by this attribute help to control the
///     batch size and timing, ensuring that messages are processed efficiently while also managing resource utilization
///     effectively.
/// </summary>
/// <param name="maxMessagesBeforeProcessing">Max number of messages to wait for before starting processing</param>
/// <param name="maxWaitTimeInSeconds">
///     Max wait time in seconds before processing the batch regardless of number of
///     messages received (at least one)
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BatchProcessingAttribute(int maxMessagesBeforeProcessing, int maxWaitTimeInSeconds) : Attribute
{
    /// <summary>
    ///     Max number of messages to wait for before starting processing
    /// </summary>
    public int MaxMessagesBeforeProcessing { get; } = maxMessagesBeforeProcessing;

    /// <summary>
    ///     Max wait time in seconds before processing the batch regardless of number of
    ///     messages received (at least one)
    /// </summary>
    public int MaxWaitTimeInSeconds { get; } = maxWaitTimeInSeconds;
}