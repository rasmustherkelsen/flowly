namespace Flowly.Transport;

/// <summary>
///     Represents a single message retrieved from a dead-letter sub-queue. Transport implementations return instances of
///     this interface from <see cref="IDeadLetterReceiver.ReceiveMessages" />. The framework maps the values to the
///     <c>DeadLetters</c> database table for tracking and requeue operations.
/// </summary>
public interface IDeadLetterMessage
{
    /// <summary>
    ///     The broker-assigned identifier for the original message.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    ///     The raw message body as a string, typically JSON. Stored verbatim in the database so that the original payload
    ///     can be inspected or re-published without re-serialisation.
    /// </summary>
    string RawBody { get; }

    /// <summary>
    ///     The application-level properties that were attached to the original message (e.g. retry count, correlation ID).
    ///     Stored as a JSON blob in the database.
    /// </summary>
    IReadOnlyDictionary<string, object> ApplicationProperties { get; }

    /// <summary>
    ///     The broker-provided reason why the message was dead-lettered (e.g. <c>"MaxDeliveryCountExceeded"</c>). May be
    ///     <see langword="null" /> if the broker does not supply a reason.
    /// </summary>
    string? DeadLetterReason { get; }

    /// <summary>
    ///     A more detailed description of the dead-letter reason supplied by the broker or by the application when it
    ///     explicitly dead-lettered the message. May be <see langword="null" />.
    /// </summary>
    string? DeadLetterErrorDescription { get; }

    /// <summary>
    ///     The UTC time at which the message was originally enqueued in the broker.
    /// </summary>
    DateTimeOffset EnqueuedTime { get; }
}