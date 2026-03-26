namespace Flowly.MessageInfrastructure.Receivers;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RetryPolicyAttribute(int maxRetries, int delaySeconds) : Attribute
{
    public int MaxRetries { get; } = maxRetries;
    
    public int DelaySeconds { get; } = delaySeconds;
}
