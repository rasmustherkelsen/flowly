using FastEndpoints;
using Flowly.Jobs.Senders;

namespace Api.Endpoints;

class RunRecurringJobEndpoint : Endpoint<RunRecurringJobEndpoint.RunRecurringJobRequest>
{
    internal sealed record RunRecurringJobRequest(Guid JobId);
    
    public IJobMessageSender MessageSender { get; set; } = null!;

    public override void Configure()
    {
        Get("/run-recurring-job");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RunRecurringJobRequest req, CancellationToken ct)
    {
        await MessageSender.StartRecurringJob(req.JobId);
        await Send.OkAsync(cancellation: ct);
    }
}
