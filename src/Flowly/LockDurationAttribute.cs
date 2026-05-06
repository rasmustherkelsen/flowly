namespace Flowly;

/// <summary>
///     Attribute to specify lock duration for message handlers. This attribute can be applied to message handler classes
///     to define the duration for which a message is locked during processing. The lock duration is used to ensure that
///     messages are not processed concurrently by multiple consumers, preventing messages from being processed multiple
///     times. The lock duration can be specified in a format that can be parsed into a TimeSpan, such as "00:00:30" for 30
///     seconds.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class LockDurationAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the LockDurationAttribute class with the specified lock duration. The lockDuration
    ///     parameter is expected to be a string that can be parsed into a TimeSpan, representing the duration for which a
    ///     message is locked during processing. If the provided string cannot be parsed into a valid TimeSpan, an
    ///     ArgumentException is thrown.
    /// </summary>
    /// <param name="lockDuration">The lock duration for which a message is locked during processing.</param>
    /// <exception cref="ArgumentException">Thrown if the provided lock duration cannot be parsed into a valid TimeSpan.</exception>
    public LockDurationAttribute(string lockDuration)
    {
        if (!TimeSpan.TryParse(lockDuration, out var parsedLockDuration))
            throw new ArgumentException($"Could not parse {nameof(lockDuration)} value '{lockDuration}' as a TimeSpan.", nameof(lockDuration));

        LockDuration = parsedLockDuration;
    }

    /// <summary>
    ///     Gets the lock duration for which a message is locked during processing.
    /// </summary>
    public TimeSpan LockDuration { get; }
}