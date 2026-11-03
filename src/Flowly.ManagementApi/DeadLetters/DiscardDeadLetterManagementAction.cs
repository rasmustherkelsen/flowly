using Flowly.DeadLetters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flowly.ManagementApi.DeadLetters;

internal sealed class DiscardDeadLetterManagementAction : IFlowlyManagementAction
{
    public string FeatureArea => "dead-letters";

    public ManagementCapability RequiredCapability => ManagementCapability.Write;

    public void MapRoute(IEndpointRouteBuilder endpoints, string basePath, string authorizationPolicyName)
    {
        endpoints.MapPost(
                $"{basePath}/{FeatureArea}/discard",
                (IDeadLetterService deadLetterService, DiscardDeadLetterRequest request, CancellationToken cancellationToken)
                    => Discard(deadLetterService, request, cancellationToken))
            .RequireAuthorization(authorizationPolicyName)
            .WithSummary("Discard a dead letter")
            .WithDescription(
                "Permanently deletes the dead letter from the tracking store; the message is not sent anywhere and cannot be recovered afterwards. " +
                "A dead letter that has already been requeued cannot be discarded (409), and 404 is returned when no dead letter has the id.")
            .WithTags(FeatureArea)
            .WithName("DiscardDeadLetter")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    internal async Task<IResult> Discard(IDeadLetterService deadLetterService, DiscardDeadLetterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            return Results.Problem(title: "Missing message id", detail: "MessageId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await deadLetterService.Discard(request.MessageId, cancellationToken);

            return Results.NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return Results.Problem(title: "Dead letter not found", detail: exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(title: "Dead letter cannot be discarded", detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
