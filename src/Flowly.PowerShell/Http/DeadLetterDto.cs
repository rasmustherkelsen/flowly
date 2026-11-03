namespace Flowly.PowerShell.Http;

/// <summary>Mirrors the JSON shape returned by the Management API's list-dead-letters action.</summary>
/// <param name="MessageId">The broker-assigned message id; the address of the dead letter for requeue and discard.</param>
/// <param name="QueueName">The queue the message was dead-lettered from, or the topic name for an event subscription dead letter.</param>
/// <param name="SubscriptionName">The event subscription the message was dead-lettered from, or <see langword="null" /> for a queue dead letter.</param>
/// <param name="MessageBody">The raw message body as stored on the broker dead letter queue.</param>
/// <param name="MessageProperties">The application properties of the message as a JSON string.</param>
/// <param name="DeadLetteredAtUtc">When the broker moved the message to the dead letter queue.</param>
/// <param name="DeadLetterReason">The broker-provided reason, or <see langword="null" /> when none was supplied.</param>
/// <param name="DeadLetterErrorDescription">The broker-provided error description, or <see langword="null" /> when none was supplied.</param>
/// <param name="Status">The status: <c>Pending</c> or <c>Requeued</c>.</param>
/// <param name="RequeuedAtUtc">When the message was requeued, or <see langword="null" /> when it has not been.</param>
/// <param name="RequeuedBy">Who requeued the message, or <see langword="null" /> when it has not been requeued or no requester was supplied.</param>
public sealed record DeadLetterDto(
    string MessageId,
    string QueueName,
    string? SubscriptionName,
    string MessageBody,
    string MessageProperties,
    DateTimeOffset DeadLetteredAtUtc,
    string? DeadLetterReason,
    string? DeadLetterErrorDescription,
    string Status,
    DateTimeOffset? RequeuedAtUtc,
    string? RequeuedBy);
