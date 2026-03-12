using FastEndpoints;
using Flowly.Jobs.Senders;
using MessageContracts;

namespace Api.Endpoints;

sealed class ProcessOrderRequest
{
    public Guid CustomerId { get; set; }
    public int? MessageCount { get; set; }
}

sealed class ProcessOrderEndpoint : Endpoint<ProcessOrderRequest>
{
    public IJobMessageSender MessageSender { get; set; } = null!;

    public override void Configure()
    {
        Get("/process-order");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ProcessOrderRequest req, CancellationToken ct)
    {
        for (int i = 0; i < (req.MessageCount ?? 1); i++)
        {
            await MessageSender.QueueJob(new ProcessOrder(req.CustomerId, $"My Stitching Operation {DateTime.UtcNow}"), ct);
        }

        await SendOkAsync(cancellation: ct);
    }
}
