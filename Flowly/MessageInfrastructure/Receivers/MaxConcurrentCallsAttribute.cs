namespace Flowly.MessageInfrastructure.Receivers;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MaxConcurrentCallsAttribute(int maxConcurrentCalls) : Attribute
{
    public int MaxConcurrentCalls { get; } = maxConcurrentCalls;
}
