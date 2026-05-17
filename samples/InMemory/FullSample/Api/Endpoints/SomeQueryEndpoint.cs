using Api.Messages;
using FastEndpoints;
using Flowly;

namespace Api.Endpoints;

internal class SomeQueryEndpoint(IMessageSender messageSender) : Endpoint<SomeQueryEndpoint.SomeQueryRequest>
{
    public override void Configure()
    {
        Get("/some-query");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SomeQueryRequest req, CancellationToken ct)
    {
        for (var i = 0; i < (req.MessageCount ?? 1); i++)
            await messageSender.Send(new SomeQueryMessage(Random.Shared.Next(1, 5)), ct);

        await Send.OkAsync(cancellation: ct);
    }

    internal record SomeQueryRequest(int? MessageCount);
}
