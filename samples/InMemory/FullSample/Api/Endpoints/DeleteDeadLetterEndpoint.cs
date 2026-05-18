using FastEndpoints;
using Flowly.DeadLetters;

namespace Api.Endpoints;

internal class DeleteDeadLetterEndpoint(IDeadLetterService deadLetterService) : Endpoint<DeleteDeadLetterEndpoint.DeleteDeadLetterRequest>
{
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

    internal record DeleteDeadLetterRequest(string MessageId);
}
