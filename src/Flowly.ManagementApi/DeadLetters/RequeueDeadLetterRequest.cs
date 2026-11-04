namespace Flowly.ManagementApi.DeadLetters;

/// <summary>The request body for requeuing a dead letter.</summary>
/// <param name="MessageId">The message id of the dead letter to requeue.</param>
/// <param name="RequeuedBy">
///     An optional identifier of the person or process requesting the requeue, stored as the audit trail of the dead letter. When it is <see langword="null" /> or blank the
///     identity of the authenticated caller is stored instead (none for an API key, which carries no identity), never a value taken from a request header.
/// </param>
internal sealed record RequeueDeadLetterRequest(string MessageId, string? RequeuedBy);
