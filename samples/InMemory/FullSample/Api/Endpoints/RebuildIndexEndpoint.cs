using Api.Messages;
using FastEndpoints;
using Flowly;

namespace Api.Endpoints;

internal class RebuildIndexEndpoint(IMessageSender messageSender) : Endpoint<RebuildIndexEndpoint.RebuildIndexRequest>
{
    public override void Configure()
    {
        Get("/rebuild-index");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RebuildIndexRequest req, CancellationToken ct)
    {
        for (var i = 0; i < (req.MessageCount ?? 1); i++)
            await messageSender.Send(new RebuildIndexMessage(DateTime.UtcNow), ct);

        await Send.OkAsync(cancellation: ct);
    }

    internal sealed record RebuildIndexRequest(int? MessageCount);
}
