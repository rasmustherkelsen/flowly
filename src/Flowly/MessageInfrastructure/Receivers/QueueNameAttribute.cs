namespace Flowly.MessageInfrastructure.Receivers;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QueueNameAttribute(string queueName) : Attribute
{
    public string QueueName { get; } = queueName;
}
