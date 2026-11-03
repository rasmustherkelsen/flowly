namespace Flowly.ManagementApi.DeadLetters;

/// <summary>The request body for requeuing a dead letter.</summary>
/// <param name="MessageId">The message id of the dead letter to requeue.</param>
/// <param name="RequeuedBy">An optional identifier of the person or process requesting the requeue, stored as the audit trail of the dead letter.</param>
internal sealed record RequeueDeadLetterRequest(string MessageId, string? RequeuedBy);
