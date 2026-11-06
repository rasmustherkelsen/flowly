using Flowly.DeadLetters;

namespace Flowly.ManagementApi.DeadLetters;

/// <summary>A tracked dead letter as returned by the list-dead-letters action.</summary>
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
/// <param name="BodyReplaced"><see langword="true" /> when the message was requeued with a replacement body instead of the stored <paramref name="MessageBody" />, which is kept unchanged.</param>
/// <param name="ReplacementMessageBody">The replacement body that was republished, or <see langword="null" /> when the message has not been requeued or was requeued with the stored body.</param>
internal sealed record DeadLetterResponse(
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
    string? RequeuedBy,
    bool BodyReplaced = false,
    string? ReplacementMessageBody = null)
{
    internal static DeadLetterResponse From(IDeadLetter deadLetter)
        => new(
            deadLetter.MessageId,
            deadLetter.QueueName,
            deadLetter.SubscriptionName,
            deadLetter.MessageBody,
            deadLetter.MessageProperties,
            deadLetter.DeadLetteredAt,
            deadLetter.DeadLetterReason,
            deadLetter.DeadLetterErrorDescription,
            deadLetter.Status.ToString(),
            deadLetter.RequeuedAt,
            deadLetter.RequeuedBy,
            deadLetter.ReplacementMessageBody is not null,
            deadLetter.ReplacementMessageBody);
}
