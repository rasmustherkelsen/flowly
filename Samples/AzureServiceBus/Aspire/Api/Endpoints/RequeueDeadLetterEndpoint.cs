using FastEndpoints;
using Flowly.DeadLetters.Services;

namespace Api.Endpoints;

class RequeueDeadLetterEndpoint : Endpoint<RequeueDeadLetterEndpoint.RequeueDeadLetterRequest>
{
    internal record RequeueDeadLetterRequest(string MessageId);

    public IDeadLetterService DeadLetterService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/dead-letters/{messageId}/requeue");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequeueDeadLetterRequest req, CancellationToken ct)
    {
        try
        {
            await DeadLetterService.Requeue(req.MessageId, cancellationToken: ct);
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
}
