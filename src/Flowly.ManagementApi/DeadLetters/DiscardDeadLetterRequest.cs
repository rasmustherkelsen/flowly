namespace Flowly.ManagementApi.DeadLetters;

/// <summary>The request body for discarding a dead letter.</summary>
/// <param name="MessageId">The message id of the dead letter to discard.</param>
internal sealed record DiscardDeadLetterRequest(string MessageId);
