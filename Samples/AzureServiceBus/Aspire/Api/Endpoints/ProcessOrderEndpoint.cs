using FastEndpoints;
using Flowly.Jobs.Senders;
using MessageContracts;

namespace Api.Endpoints;

class ProcessOrderEndpoint : Endpoint<ProcessOrderEndpoint.ProcessOrderRequest>
{
    internal record ProcessOrderRequest(Guid CustomerId, int? MessageCount);

    public IJobMessageSender MessageSender { get; set; } = null!;

    public override void Configure()
    {
        Get("/process-order");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ProcessOrderRequest req, CancellationToken cancellationToken)
    {
        for (int i = 0; i < (req.MessageCount ?? 1); i++)
        {
            await MessageSender.QueueJob(new ProcessOrder(req.CustomerId, $"Order Submitted at {DateTime.UtcNow}"), cancellationToken);
        }

        await Send.OkAsync(cancellation: cancellationToken);
    }
}