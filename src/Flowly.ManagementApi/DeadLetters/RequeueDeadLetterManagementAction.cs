using Flowly.DeadLetters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flowly.ManagementApi.DeadLetters;

internal sealed class RequeueDeadLetterManagementAction : IFlowlyManagementAction
{
    public string FeatureArea => "dead-letters";

    public ManagementCapability RequiredCapability => ManagementCapability.Write;

    public void MapRoute(IEndpointRouteBuilder endpoints, string basePath, string authorizationPolicyName)
    {
        endpoints.MapPost(
                $"{basePath}/{FeatureArea}/requeue",
                (IDeadLetterService deadLetterService, RequeueDeadLetterRequest request, CancellationToken cancellationToken)
                    => Requeue(deadLetterService, request, cancellationToken))
            .RequireAuthorization(authorizationPolicyName)
            .WithSummary("Requeue a dead letter")
            .WithDescription(
                "Sends the dead letter back to its original queue (or, for an event subscription dead letter, to the topic addressed to the originating subscriber only) " +
                "and marks it Requeued, with the optional requester stored as the audit trail. The Flowly retry count is cleared, so the message gets a fresh set of retries. " +
                "Only a Pending dead letter can be requeued (409 otherwise, 404 when no dead letter has the id).")
            .WithTags(FeatureArea)
            .WithName("RequeueDeadLetter")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    internal async Task<IResult> Requeue(IDeadLetterService deadLetterService, RequeueDeadLetterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            return Results.Problem(title: "Missing message id", detail: "MessageId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await deadLetterService.Requeue(request.MessageId, request.RequeuedBy, cancellationToken);

            return Results.NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return Results.Problem(title: "Dead letter not found", detail: exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(title: "Dead letter cannot be requeued", detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
