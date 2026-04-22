using FastEndpoints;
using Flowly.Jobs.Senders;

namespace Api.Endpoints;

class RunRecurringJobEndpoint(IJobMessageSender jobMessageSender) : Endpoint<RunRecurringJobEndpoint.RunRecurringJobRequest>
{
    internal sealed record RunRecurringJobRequest(Guid JobId);

    public override void Configure()
    {
        Get("/run-recurring-job");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RunRecurringJobRequest req, CancellationToken ct)
    {
        await jobMessageSender.StartRecurringJob(req.JobId);
        await Send.OkAsync(cancellation: ct);
    }
}
