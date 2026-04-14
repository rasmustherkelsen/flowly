namespace Flowly.MessagingAbstractions;

public class MessageTooLargeException : Exception
{
    public MessageTooLargeException(string queueName, long actualSizeBytes, long maxSizeBytes)
        : base($"Message for queue '{queueName}' is {actualSizeBytes:N0} bytes, which exceeds the configured limit of {maxSizeBytes:N0} bytes.")
    {
        QueueName = queueName;
        ActualSizeBytes = actualSizeBytes;
        MaxSizeBytes = maxSizeBytes;
    }
    
    public string QueueName { get; }
    public long ActualSizeBytes { get; }
    public long MaxSizeBytes { get; }
}
