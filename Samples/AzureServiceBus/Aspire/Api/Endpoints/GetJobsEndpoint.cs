using FastEndpoints;
using Flowly.Jobs.Model;
using Flowly.Jobs.Services;

namespace Api.Endpoints;

internal record JobDto(
    Guid JobIdentifier,
    string JobTypeName,
    string Description,
    string CurrentState,
    DateTimeOffset Created,
    DateTimeOffset? Started,
    DateTimeOffset? Completed,
    string? FaultReason,
    int RetryAttempt,
    bool IsRecurringJob,
    string? CronExpression);

class GetJobsEndpoint : Endpoint<GetJobsEndpoint.GetJobsRequest, PagedResult<JobDto>>
{
    internal record GetJobsRequest(int Page = 1, int PageSize = 20, string? Status = null, bool? IsRecurringJob = null);

    public IJobTrackingService JobTrackingService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/jobs");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetJobsRequest req, CancellationToken ct)
    {
        if (req.IsRecurringJob == true)
        {
            var recurringJobs = await JobTrackingService.GetRecurringJobs(ct);

            var items = recurringJobs
                .OrderByDescending(j => j.LastCompleted ?? j.Created)
                .Select(j => new JobDto(
                    j.JobId,
                    j.JobTypeName,
                    j.Description,
                    "Recurring",
                    j.Created,
                    j.LastStarted,
                    j.LastCompleted,
                    null,
                    0,
                    true,
                    j.CronExpression))
                .ToArray();

            await Send.OkAsync(new PagedResult<JobDto>(items, items.Length, 1, items.Length), ct);
            return;
        }

        var jobs = await JobTrackingService.GetJobs(ct);

        var query = jobs.AsEnumerable();

        if (req.Status is not null && Enum.TryParse<JobState>(req.Status, ignoreCase: true, out var parsedState))
            query = query.Where(j => j.CurrentState == parsedState);

        var totalCount = query.Count();

        int page = req.Page > 0 ? req.Page : 1;
        int pageSize = req.PageSize > 0 ? req.PageSize : 20;

        var pageItems = query
            .OrderByDescending(j => j.Created)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobDto(
                j.JobIdentifier,
                j.JobTypeName,
                j.Description,
                j.CurrentState.ToString(),
                j.Created,
                j.Started,
                j.Completed,
                j.FaultReason,
                j.RetryAttempt,
                false,
                null))
            .ToArray();

        await Send.OkAsync(new PagedResult<JobDto>(pageItems, totalCount, page, pageSize), ct);
    }
}
