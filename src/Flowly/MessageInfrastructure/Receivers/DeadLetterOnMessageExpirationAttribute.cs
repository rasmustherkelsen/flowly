namespace Flowly.MessageInfrastructure.Receivers;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DeadLetterOnMessageExpirationAttribute(bool enabled) : Attribute
{
    public bool Enabled { get; } = enabled;
}
