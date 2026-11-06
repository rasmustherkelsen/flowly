using Flowly.DeadLetters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flowly.ManagementApi.DeadLetters;

internal sealed class ListDeadLettersManagementAction : IFlowlyManagementAction
{
    internal const int MaxPageSize = 1000;

    public string FeatureArea => "dead-letters";

    public ManagementCapability RequiredCapability => ManagementCapability.Read;

    public void MapRoute(IEndpointRouteBuilder endpoints, string basePath, string authorizationPolicyName)
    {
        endpoints.MapGet(
                $"{basePath}/{FeatureArea}",
                (IDeadLetterService deadLetterService, string? queue, string? subscription, string? status, string? messageId, int? skip, int? take, CancellationToken cancellationToken)
                    => List(deadLetterService, queue, subscription, status, messageId, skip, take, cancellationToken))
            .RequireAuthorization(authorizationPolicyName)
            .WithSummary("List the tracked dead letters")
            .WithDescription(
                "Returns the tracked dead letters (message id, queue or topic, subscription, body, properties, broker reason, status, requeue audit, and for a message requeued with a corrected body bodyReplaced and the replacementMessageBody), newest first, " +
                "optionally filtered by queue (the topic name for an event subscription dead letter), subscription, status (Pending or Requeued) and/or message id, " +
                $"and paged with skip and take (at most {MaxPageSize} per page; without take a page of {DeadLetterQuery.DefaultTake} is returned, so page through a longer list with skip).")
            .WithTags(FeatureArea)
            .WithName("ListDeadLetters")
            .Produces<IReadOnlyList<DeadLetterResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    internal async Task<IResult> List(
        IDeadLetterService deadLetterService,
        string? queue,
        string? subscription,
        string? status,
        string? messageId,
        int? skip,
        int? take,
        CancellationToken cancellationToken)
    {
        DeadLetterStatus? statusFilter = null;

        if (status is not null)
        {
            if (!Enum.TryParse<DeadLetterStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return Results.Problem(
                    title: "Unknown dead letter status",
                    detail: $"'{status}' is not a dead letter status. Use one of: {string.Join(", ", Enum.GetNames<DeadLetterStatus>())}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            statusFilter = parsed;
        }

        if (skip is < 0)
        {
            return Results.Problem(title: "Invalid skip", detail: "skip must be zero or greater.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (take is < 1 or > MaxPageSize)
        {
            return Results.Problem(title: "Invalid take", detail: $"take must be between 1 and {MaxPageSize}.", statusCode: StatusCodes.Status400BadRequest);
        }

        var page = await deadLetterService.QueryDeadLetters(
            new DeadLetterQuery
            {
                QueueName = queue,
                SubscriptionName = subscription,
                Status = statusFilter,
                MessageId = messageId,
                Skip = skip ?? 0,
                Take = take
            },
            cancellationToken);

        return Results.Ok(page.Items.Select(DeadLetterResponse.From).ToList());
    }
}
