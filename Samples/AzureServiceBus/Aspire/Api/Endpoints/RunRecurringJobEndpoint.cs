using FastEndpoints;
using Flowly.Jobs.Senders;

namespace Api.Endpoints;

sealed class RunRecurringJobRequest
{
    public Guid JobId { get; set; }
}

sealed class RunRecurringJobEndpoint : Endpoint<RunRecurringJobRequest>
{
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
