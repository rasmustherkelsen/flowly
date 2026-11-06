namespace Flowly.ManagementApi.DeadLetters;

/// <summary>The request body for requeuing a dead letter.</summary>
/// <param name="MessageId">The message id of the dead letter to requeue.</param>
/// <param name="RequeuedBy">
///     An optional identifier of the person or process requesting the requeue, stored as the audit trail of the dead letter only when the caller has no authenticated identity of its
///     own (an API key carries none). The identity of an authenticated caller always wins over this value, so a caller cannot record the requeue under another name, and no value is
///     ever taken from a request header.
/// </param>
/// <param name="MessageBody">
///     An optional replacement for the stored message body: the message is republished with this body instead, and the stored body is kept in the dead letter record next to it.
///     <see langword="null" /> or absent republishes the stored body as it is; an empty or whitespace value, a value larger than 256 KiB as UTF-8, or a non-JSON value for a JSON stored body is rejected with 400.
/// </param>
internal sealed record RequeueDeadLetterRequest(string MessageId, string? RequeuedBy, string? MessageBody = null);
