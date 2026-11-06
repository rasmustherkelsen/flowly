using Flowly.MessageInfrastructure.StreamPartitions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flowly.ManagementApi.MessageStreams;

internal sealed class ListStreamPartitionsManagementAction(IStreamPartitionStateReader stateReader) : IFlowlyManagementAction
{
    public string FeatureArea => "message-streams";

    public ManagementCapability RequiredCapability => ManagementCapability.Read;

    public void MapRoute(IEndpointRouteBuilder endpoints, string basePath, string authorizationPolicyName)
    {
        endpoints.MapGet($"{basePath}/{FeatureArea}/partitions", (string? queue, string? consumer, int? partition, string? state) => List(queue, consumer, partition, state))
            .RequireAuthorization(authorizationPolicyName)
            .WithSummary("List the stream partitions owned by this instance")
            .WithDescription(
                "Returns the state (Running, Waiting or Halted), the last error and the time of the last state change of every message stream partition " +
                "owned by the instance that answers, optionally filtered by queue, consumer, partition and/or state. A non-partitioned stream is one partition " +
                "with a null partition. Each instance reports only its own partitions: with several instances, address the one that owns the partition.")
            .WithTags(FeatureArea)
            .WithName("ListMessageStreamPartitions")
            .Produces<IReadOnlyList<StreamPartitionResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    internal IResult List(string? queue, string? consumer, int? partition, string? state)
    {
        StreamPartitionState? stateFilter = null;

        if (state is not null)
        {
            if (!Enum.TryParse<StreamPartitionState>(state, true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return Results.Problem(
                    title: "Unknown partition state",
                    detail: $"'{state}' is not a partition state. Use one of: {string.Join(", ", Enum.GetNames<StreamPartitionState>())}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            stateFilter = parsed;
        }

        var partitions = stateReader.GetAll()
            .Where(status => queue is null || status.Key.QueueName == queue)
            .Where(status => consumer is null || status.Key.ConsumerName == consumer)
            .Where(status => partition is null || status.Key.Partition == partition)
            .Where(status => stateFilter is null || status.State == stateFilter)
            .OrderBy(status => status.Key.QueueName, StringComparer.Ordinal)
            .ThenBy(status => status.Key.ConsumerName, StringComparer.Ordinal)
            .ThenBy(status => status.Key.Partition)
            .Select(StreamPartitionResponse.From)
            .ToList();

        return Results.Ok(partitions);
    }
}
