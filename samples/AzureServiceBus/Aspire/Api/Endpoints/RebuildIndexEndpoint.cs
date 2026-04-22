using FastEndpoints;
using Flowly;
using MessageContracts;

namespace Api.Endpoints;

class RebuildIndexEndpoint(IMessageSender messageSender) : Endpoint<RebuildIndexEndpoint.RebuildIndexRequest>
{
    internal sealed record RebuildIndexRequest(int? MessageCount);

    public override void Configure()
    {
        Get("/rebuild-index");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RebuildIndexRequest req, CancellationToken ct)
    {
        for (int i = 0; i < (req.MessageCount ?? 1); i++)
        {
            await messageSender.Send(new RebuildIndexMessage(DateTime.UtcNow));
        }

        await Send.OkAsync(cancellation: ct);
    }
}
