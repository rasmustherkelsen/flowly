using Flowly.MessageStreams;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flowly.ManagementApi.MessageStreams;

internal sealed class ResetCheckpointManagementAction(MessageStreamCheckpointStoreRegistry registry) : IFlowlyManagementAction
{
    public string FeatureArea => "message-streams";

    public ManagementCapability RequiredCapability => ManagementCapability.Write;

    public void MapRoute(IEndpointRouteBuilder endpoints, string basePath, string authorizationPolicyName)
    {
        endpoints.MapPost($"{basePath}/{FeatureArea}/checkpoints/reset", async (ResetCheckpointRequest request, CancellationToken cancellationToken) =>
            {
                var dbContextFactory = registry.Resolve(request.Store);
                await MessageStreamCheckpointAdmin.Reset(
                    dbContextFactory, request.QueueName, request.ConsumerName, request.Partition, request.Offset, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(authorizationPolicyName)
            .WithSummary("Reset a message stream checkpoint's position")
            .WithDescription(
                "Sets the stored position for the given queue/consumer/partition to the given offset. The running " +
                "handler only picks this up on its next restart — resetting to an earlier offset causes those " +
                "messages to be reprocessed.")
            .WithTags(FeatureArea)
            .WithName("ResetMessageStreamCheckpoint");
    }
}
