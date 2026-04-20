namespace Flowly.MessageInfrastructure.Receivers;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class LockDurationAttribute(string lockDuration) : Attribute
{
    public string LockDuration { get; } = lockDuration;

    public TimeSpan GetValue()
    {
        if (TimeSpan.TryParse(LockDuration, out var parsedLockDuration))
        {
            return parsedLockDuration;
        }

        throw new InvalidOperationException($"Could not parse {nameof(LockDuration)} value '{LockDuration}' as a TimeSpan.");
    }
}
