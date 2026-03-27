using FastEndpoints;
using Flowly.Jobs.DatabaseModel;
using Flowly.Jobs.Model;
using Microsoft.EntityFrameworkCore;

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

    public IDbContextFactory<JobStateDataContext> ContextFactory { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/jobs");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetJobsRequest req, CancellationToken ct)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var query = context.Jobs.AsNoTracking();

        if (req.Status is not null && Enum.TryParse<JobState>(req.Status, ignoreCase: true, out var parsedState))
        {
            query = query.Where(j => j.CurrentState == parsedState);
        }

        if (req.IsRecurringJob is not null)
        {
            query = query.Where(j => j.IsRecurringJob == req.IsRecurringJob);
        }

        var totalCount = await query.CountAsync(ct);

        int page = req.Page > 0 ? req.Page : 1;
        int pageSize = req.PageSize > 0 ? req.PageSize : 20;

        var items = await query
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
                j.IsRecurringJob,
                j.CronExpression))
            .ToArrayAsync(ct);

        await Send.OkAsync(new PagedResult<JobDto>(items, totalCount, page, pageSize), ct);
    }
}
