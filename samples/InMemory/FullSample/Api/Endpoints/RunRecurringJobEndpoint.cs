using FastEndpoints;
using Flowly.Jobs;

namespace Api.Endpoints;

internal class RunRecurringJobEndpoint(IJobMessageSender jobMessageSender) : Endpoint<RunRecurringJobEndpoint.RunRecurringJobRequest>
{
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

    internal sealed record RunRecurringJobRequest(Guid JobId);
}
