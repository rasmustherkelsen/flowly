using FastEndpoints;
using Flowly.DeadLetters;

namespace Api.Endpoints;

internal class RequeueDeadLetterEndpoint(IDeadLetterService deadLetterService) : Endpoint<RequeueDeadLetterEndpoint.RequeueDeadLetterRequest>
{
    public override void Configure()
    {
        Post("/api/dead-letters/{messageId}/requeue");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequeueDeadLetterRequest req, CancellationToken ct)
    {
        try
        {
            await deadLetterService.Requeue(req.MessageId, cancellationToken: ct);
            await Send.OkAsync(cancellation: ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            await HttpContext.Response.SendAsync(new { error = ex.Message }, 409, cancellation: ct);
        }
    }

    internal record RequeueDeadLetterRequest(string MessageId);
}
