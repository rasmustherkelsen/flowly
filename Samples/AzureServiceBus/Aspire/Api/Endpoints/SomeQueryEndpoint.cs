using FastEndpoints;
using Flowly.MessageInfrastructure.Senders;
using MessageContracts;

namespace Api.Endpoints;

class SomeQueryEndpoint(IMessageSender messageSender) : Endpoint<SomeQueryEndpoint.SomeQueryRequest>
{
    internal record SomeQueryRequest(int? MessageCount);
    
    public override void Configure()
    {
        Get("/some-query");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SomeQueryRequest req, CancellationToken ct)
    {
        for (int i = 0; i < (req.MessageCount ?? 1); i++)
        {
            await messageSender.Send(new SomeQueryMessage(Random.Shared.Next(1, 5)), ct);
        }

        await Send.OkAsync(cancellation: ct);
    }
}