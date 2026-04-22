using FastEndpoints;
using Flowly.Jobs.Senders;
using MessageContracts;

namespace Api.Endpoints;

class ProcessOrderEndpoint(IJobMessageSender jobMessageSender) : Endpoint<ProcessOrderEndpoint.ProcessOrderRequest>
{
    internal record ProcessOrderRequest(int? MessageCount);

    public override void Configure()
    {
        Get("/process-order");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ProcessOrderRequest req, CancellationToken cancellationToken)
    {
        for (int i = 0; i < (req.MessageCount ?? 1); i++)
        {
            await jobMessageSender.QueueJob(new ProcessOrder(Guid.NewGuid(), $"Order Submitted at {DateTime.UtcNow}"), cancellationToken);
        }

        await Send.OkAsync(cancellation: cancellationToken);
    }
}