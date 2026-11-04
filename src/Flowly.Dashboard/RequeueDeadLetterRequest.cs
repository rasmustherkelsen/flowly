namespace Flowly.Dashboard;

/// <summary>The optional JSON body of the dashboard requeue endpoint.</summary>
/// <param name="MessageBody">
///     The replacement for the stored message body, or <see langword="null" /> to republish the stored body as it is. An empty or whitespace value is rejected with 400.
/// </param>
internal sealed record RequeueDeadLetterRequest(string? MessageBody);
