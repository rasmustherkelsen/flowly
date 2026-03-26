using FastEndpoints;
using Flowly.MessageInfrastructure.Senders;
using MessageContracts;

namespace Api.Endpoints;

class RebuildIndexEndpoint : Endpoint<RebuildIndexEndpoint.RebuildIndexRequest>
{
    internal sealed record RebuildIndexRequest(int? MessageCount);
    
    public IMessageSender MessageSender { get; set; } = null!;

    public override void Configure()
    {
        Get("/rebuild-index");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RebuildIndexRequest req, CancellationToken ct)
    {
        for (int i = 0; i < (req.MessageCount ?? 1); i++)
        {
            await MessageSender.Send(new RebuildIndexMessage(DateTime.UtcNow));
        }

        await Send.OkAsync(cancellation: ct);
    }
}
