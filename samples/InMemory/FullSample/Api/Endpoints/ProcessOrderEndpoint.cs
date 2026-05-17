using Api.Messages;
using FastEndpoints;
using Flowly.Jobs;

namespace Api.Endpoints;

internal class ProcessOrderEndpoint(IJobMessageSender jobMessageSender) : Endpoint<ProcessOrderEndpoint.ProcessOrderRequest>
{
    public override void Configure()
    {
        Get("/process-order");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ProcessOrderRequest req, CancellationToken cancellationToken)
    {
        for (var i = 0; i < (req.MessageCount ?? 1); i++)
            await jobMessageSender.QueueJob(new ProcessOrder(Guid.NewGuid(), $"Order Submitted at {DateTime.UtcNow}"), cancellationToken);

        await Send.OkAsync(cancellation: cancellationToken);
    }

    internal record ProcessOrderRequest(int? MessageCount);
}
