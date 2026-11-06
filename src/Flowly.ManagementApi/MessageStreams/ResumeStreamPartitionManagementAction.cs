using Flowly.MessageInfrastructure.StreamPartitions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flowly.ManagementApi.MessageStreams;

internal sealed class ResumeStreamPartitionManagementAction(IStreamPartitionStateReader stateReader, IStreamPartitionResumer resumer) : IFlowlyManagementAction
{
    public string FeatureArea => "message-streams";

    public ManagementCapability RequiredCapability => ManagementCapability.Write;

    public void MapRoute(IEndpointRouteBuilder endpoints, string basePath, string authorizationPolicyName)
    {
        endpoints.MapPost($"{basePath}/{FeatureArea}/partitions/resume", (ResumeStreamPartitionRequest request) => Resume(request))
            .RequireAuthorization(authorizationPolicyName)
            .WithSummary("Resume a halted stream partition")
            .WithDescription(
                "Re-runs the batch a halted partition is holding, with a fresh set of in-process retries, and lets the partition continue from it. " +
                "Nothing is skipped: when the batch fails again the partition halts again. Works only on the instance that owns the partition " +
                "(404 when this instance does not own it) and only on a Halted partition (409 otherwise; a Waiting partition is retried automatically).")
            .WithTags(FeatureArea)
            .WithName("ResumeMessageStreamPartition")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    internal IResult Resume(ResumeStreamPartitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QueueName) || string.IsNullOrWhiteSpace(request.ConsumerName))
        {
            return Results.Problem(
                title: "Missing partition address",
                detail: "QueueName and ConsumerName are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var key = new StreamPartitionKey(request.QueueName, request.ConsumerName, request.Partition);
        var status = stateReader.Get(key);

        if (status is null)
            return NotOwned(key);

        if (status.State != StreamPartitionState.Halted)
            return NotHalted(key, status.State);

        return resumer.Resume(key) switch
        {
            StreamPartitionResumeResult.Resumed => Results.NoContent(),
            StreamPartitionResumeResult.NotHalted => NotHalted(key, StreamPartitionState.Running),
            _ => NotOwned(key)
        };
    }

    private static IResult NotOwned(StreamPartitionKey key)
        => Results.Problem(
            title: "Partition not owned by this instance",
            detail: $"This instance does not own {Describe(key)}. Address the instance that owns the partition.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult NotHalted(StreamPartitionKey key, StreamPartitionState state)
        => Results.Problem(
            title: "Partition is not halted",
            detail: state == StreamPartitionState.Waiting
                ? $"{Describe(key)} is {state}, not Halted: it has no processing loop yet and is retried automatically, so there is nothing to resume."
                : $"{Describe(key)} is {state}, not Halted, so there is nothing to resume.",
            statusCode: StatusCodes.Status409Conflict);

    private static string Describe(StreamPartitionKey key)
        => key.Partition is { } partition
            ? $"partition {partition} of stream queue '{key.QueueName}' for consumer '{key.ConsumerName}'"
            : $"stream queue '{key.QueueName}' for consumer '{key.ConsumerName}'";
}
