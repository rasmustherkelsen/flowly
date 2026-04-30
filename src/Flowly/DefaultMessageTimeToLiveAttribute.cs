namespace Flowly;

/// <summary>
///     Attribute to specify the default time-to-live for messages handled by a message handler class. This attribute can
///     be applied to message handler classes to define the default duration that messages should remain in the queue
///     before they expire. The time-to-live (TTL) setting helps to manage message lifecycle and ensures that messages that
///     are not processed within a certain timeframe are automatically removed from the queue, preventing stale messages
///     from accumulating and improving overall system performance. The TTL value can be specified in a format that can be
///     parsed into a TimeSpan, such as "00:30:00" for 30 minutes or "1.00:00:00" for 1 day.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DefaultMessageTimeToLiveAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the DefaultMessageTimeToLiveAttribute class with the specified time-to-live duration.
    ///     The timeToLive parameter is expected to be a string that can be parsed into a TimeSpan, representing the duration
    ///     that messages should remain in the queue before they expire. If the provided string cannot be parsed into a valid
    ///     TimeSpan, an InvalidOperationException is thrown, indicating that the value is not in the correct format. This
    ///     constructor ensures that the TimeToLive property is properly initialized with a valid TimeSpan value based on the
    ///     input string.
    /// </summary>
    /// <param name="timeToLive">Time to live in a string format that can be parsed into a TimeSpan.</param>
    /// <exception cref="ArgumentException">If timeToLive cannot be parsed into a valid TimeSpan.</exception>
    public DefaultMessageTimeToLiveAttribute(string timeToLive)
    {
        if (!TimeSpan.TryParse(timeToLive, out var parsedTimeToLive))
            throw new ArgumentException($"Could not parse {nameof(timeToLive)} value '{timeToLive}' as a TimeSpan.");

        TimeToLive = parsedTimeToLive;
    }

    /// <summary>
    ///     Gets the default time-to-live duration for messages handled by the message handler class. This value is parsed from
    ///     the string provided in the constructor and represents the duration that messages should remain in the queue before
    ///     they expire. The TimeToLive property is of type TimeSpan and can be used to configure the message queue's TTL
    ///     settings for messages processed by the handler class.
    /// </summary>
    public TimeSpan TimeToLive { get; }
}