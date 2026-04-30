namespace Flowly.DeadLetters;

/// <summary>
///     Represents a dead-lettered message that has been captured and persisted by Flowly's dead letter tracking system.
///     A dead letter arises when a message cannot be successfully processed after all retry attempts are exhausted.
/// </summary>
public interface IDeadLetter
{
    /// <summary>
    ///     The unique identifier of the message, as assigned by the broker.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    ///     The name of the queue the message was dead-lettered from.
    ///     For event subscription dead letters this holds the topic name; see <see cref="SubscriptionName"/>.
    /// </summary>
    string QueueName { get; }

    /// <summary>
    ///     The name of the event subscription the message was dead-lettered from.
    ///     Only set for event subscription dead letters; <see langword="null"/> for regular queue dead letters.
    ///     When set, <see cref="QueueName"/> holds the topic name.
    /// </summary>
    string? SubscriptionName { get; }

    /// <summary>
    ///     The raw message body exactly as it was stored on the broker dead letter queue.
    /// </summary>
    string MessageBody { get; }

    /// <summary>
    ///     The application properties of the message serialized as a JSON string.
    ///     Includes broker metadata and any Flowly-specific properties such as retry counts.
    /// </summary>
    string MessageProperties { get; }

    /// <summary>
    ///     The timestamp at which the message was moved to the dead letter queue by the broker.
    /// </summary>
    DateTimeOffset DeadLetteredAt { get; }

    /// <summary>
    ///     The broker-provided reason for dead-lettering the message (e.g. <c>MaxDeliveryCountExceeded</c>).
    ///     <see langword="null"/> when the broker did not supply a reason.
    /// </summary>
    string? DeadLetterReason { get; }

    /// <summary>
    ///     A detailed error description provided by the broker when the message was dead-lettered.
    ///     <see langword="null"/> when no error description was provided.
    /// </summary>
    string? DeadLetterErrorDescription { get; }

    /// <summary>
    ///     The current lifecycle status of this dead letter record.
    ///     Starts as <see cref="DeadLetterStatus.Pending"/> and transitions to
    ///     <see cref="DeadLetterStatus.Requeued"/> once the message is sent back to its original queue or topic.
    /// </summary>
    DeadLetterStatus Status { get; }

    /// <summary>
    ///     The timestamp at which this message was requeued back to its original queue or topic.
    ///     <see langword="null"/> when the message has not yet been requeued.
    /// </summary>
    DateTimeOffset? RequeuedAt { get; }

    /// <summary>
    ///     An optional identifier of the person or process that requeued this message.
    ///     <see langword="null"/> when the message has not been requeued or no requester identity was supplied.
    /// </summary>
    string? RequeuedBy { get; }
}