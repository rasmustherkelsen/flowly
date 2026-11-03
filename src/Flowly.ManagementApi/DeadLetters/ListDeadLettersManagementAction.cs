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
                "Returns the tracked dead letters (message id, queue or topic, subscription, body, properties, broker reason, status, requeue audit), newest first, " +
                "optionally filtered by queue (the topic name for an event subscription dead letter), subscription, status (Pending or Requeued) and/or message id, " +
                $"and paged with skip and take (at most {MaxPageSize} per page; without take everything after skip is returned).")
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

        var deadLetters = await deadLetterService.GetDeadLetters(cancellationToken);

        var page = deadLetters
            .Where(deadLetter => queue is null || deadLetter.QueueName == queue)
            .Where(deadLetter => subscription is null || deadLetter.SubscriptionName == subscription)
            .Where(deadLetter => statusFilter is null || deadLetter.Status == statusFilter)
            .Where(deadLetter => messageId is null || deadLetter.MessageId == messageId)
            .OrderByDescending(deadLetter => deadLetter.DeadLetteredAt)
            .ThenBy(deadLetter => deadLetter.MessageId, StringComparer.Ordinal)
            .Skip(skip ?? 0)
            .Take(take ?? int.MaxValue)
            .Select(DeadLetterResponse.From)
            .ToList();

        return Results.Ok(page);
    }
}
