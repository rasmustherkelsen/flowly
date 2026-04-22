using FastEndpoints;
using Flowly.DeadLetters.Services;

namespace Api.Endpoints;

class DeleteDeadLetterEndpoint(IDeadLetterService deadLetterService) : Endpoint<DeleteDeadLetterEndpoint.DeleteDeadLetterRequest>
{
    internal record DeleteDeadLetterRequest(string MessageId);

    public override void Configure()
    {
        Delete("/api/dead-letters/{messageId}/discard");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DeleteDeadLetterRequest req, CancellationToken ct)
    {
        try
        {
            await deadLetterService.Discard(req.MessageId, ct);
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
