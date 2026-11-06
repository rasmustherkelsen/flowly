using System.Security.Claims;
using Flowly.DeadLetters;
using Flowly.ManagementApi.Authorization;
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
                (IDeadLetterService deadLetterService, RequeueDeadLetterRequest request, ClaimsPrincipal user, ManagementApiOptions options, CancellationToken cancellationToken)
                    => Requeue(deadLetterService, request, user, options.AuthMode, cancellationToken))
            .RequireAuthorization(authorizationPolicyName)
            .WithSummary("Requeue a dead letter")
            .WithDescription(
                "Sends the dead letter back to its original queue (or, for an event subscription dead letter, to the topic addressed to the originating subscriber only) " +
                "and marks it Requeued, with the requester stored as the audit trail: the authenticated caller's identity, which always wins over the request, and only when the caller has none (an API key carries no identity) the request's optional requeuedBy. That identity is the principal's name for host authorization policies and the principal's name, then its sub or azp claim, for OAuth2. The Flowly retry count is cleared, so the message gets a fresh set of retries. " +
                "The optional messageBody republishes a corrected body instead of the stored one: the stored body stays in the record, which then reports bodyReplaced and the replacement, and nothing is recorded when the publish fails. " +
                "A replacement must not be empty or whitespace, must not exceed 256 KiB as UTF-8 and must be valid JSON when the stored body is JSON (400 otherwise). " +
                "Only a Pending dead letter can be requeued (409 otherwise, 404 when no dead letter has the id). Any other failure, such as a missing transport provider, is a server error.")
            .WithTags(FeatureArea)
            .WithName("RequeueDeadLetter")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    internal async Task<IResult> Requeue(
        IDeadLetterService deadLetterService,
        RequeueDeadLetterRequest request,
        ClaimsPrincipal user,
        ManagementApiAuthMode authMode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            return Results.Problem(title: "Missing message id", detail: "MessageId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var requeuedBy = CallerIdentityResolver.Resolve(user, authMode) ?? (string.IsNullOrWhiteSpace(request.RequeuedBy) ? null : request.RequeuedBy);

        try
        {
            await deadLetterService.Requeue(request.MessageId, requeuedBy, request.MessageBody, cancellationToken);

            return Results.NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return Results.Problem(title: "Dead letter not found", detail: exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidReplacementMessageBodyException exception)
        {
            return Results.Problem(title: "Invalid replacement message body", detail: exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DeadLetterStatusConflictException exception)
        {
            return Results.Problem(title: "Dead letter cannot be requeued", detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
