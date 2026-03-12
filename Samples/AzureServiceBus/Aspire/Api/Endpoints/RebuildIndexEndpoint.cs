using FastEndpoints;
using Flowly.MessageInfrastructure.Senders;
using MessageContracts;

namespace Api.Endpoints;

sealed class RebuildIndexRequest
{
    public int? MessageCount { get; set; }
}

sealed class RebuildIndexEndpoint : Endpoint<RebuildIndexRequest>
{
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

        await SendOkAsync(cancellation: ct);
    }
}
