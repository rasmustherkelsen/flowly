using Flowly.MessageStreams;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flowly.ManagementApi.MessageStreams;

internal sealed class ListCheckpointsManagementAction(MessageStreamCheckpointStoreRegistry registry) : IFlowlyManagementAction
{
    public string FeatureArea => "message-streams";

    public ManagementCapability RequiredCapability => ManagementCapability.Read;

    public void MapRoute(IEndpointRouteBuilder endpoints, string basePath, string authorizationPolicyName)
    {
        endpoints.MapGet($"{basePath}/{FeatureArea}/checkpoints", async (string? queue, string? consumer, string? store, CancellationToken cancellationToken) =>
            {
                var dbContextFactory = registry.Resolve(store);
                var checkpoints = await MessageStreamCheckpointAdmin.List(dbContextFactory, queue, consumer, cancellationToken);
                return Results.Ok(checkpoints);
            })
            .RequireAuthorization(authorizationPolicyName)
            .WithSummary("List message stream checkpoint positions")
            .WithDescription("Returns every stored checkpoint row (queue, consumer, partition, position, last updated), optionally filtered by queue and/or consumer.")
            .WithTags(FeatureArea)
            .WithName("ListMessageStreamCheckpoints");
    }
}
