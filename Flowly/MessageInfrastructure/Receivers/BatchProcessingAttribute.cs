namespace Flowly.MessageInfrastructure.Receivers;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BatchProcessingAttribute(int maxMessagesBeforeProcessing, int maxWaitTimeInSeconds) : Attribute
{
    public int MaxMessagesBeforeProcessing { get; } = maxMessagesBeforeProcessing;

    public int MaxWaitTimeInSeconds { get; } = maxWaitTimeInSeconds;
}
