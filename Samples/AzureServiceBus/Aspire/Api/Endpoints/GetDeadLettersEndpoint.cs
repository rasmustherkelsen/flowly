using FastEndpoints;
using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Services;

namespace Api.Endpoints;

internal record DeadLetterDto(
    string MessageId,
    string QueueName,
    string MessageBody,
    string MessageProperties,
    DateTimeOffset DeadLetteredAt,
    string? DeadLetterReason,
    string? DeadLetterErrorDescription,
    string Status,
    DateTimeOffset? RequeuedAt,
    string? RequeuedBy);

class GetDeadLettersEndpoint : Endpoint<GetDeadLettersEndpoint.GetDeadLettersRequest, PagedResult<DeadLetterDto>>
{
    internal record GetDeadLettersRequest(int Page = 1, int PageSize = 20, string? Status = null);

    public IDeadLetterService DeadLetterService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/dead-letters");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetDeadLettersRequest req, CancellationToken ct)
    {
        var allDeadLetters = await DeadLetterService.GetDeadLetters(ct);

        var query = allDeadLetters.AsEnumerable();

        if (req.Status is not null && Enum.TryParse<DeadLetterStatus>(req.Status, ignoreCase: true, out var parsedStatus))
            query = query.Where(d => d.Status == parsedStatus);

        var totalCount = query.Count();

        int page = req.Page > 0 ? req.Page : 1;
        int pageSize = req.PageSize > 0 ? req.PageSize : 20;

        var items = query
            .OrderByDescending(d => d.DeadLetteredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DeadLetterDto(
                d.MessageId,
                d.QueueName,
                d.MessageBody,
                d.MessageProperties,
                d.DeadLetteredAt,
                d.DeadLetterReason,
                d.DeadLetterErrorDescription,
                d.Status.ToString(),
                d.RequeuedAt,
                d.RequeuedBy))
            .ToArray();

        await Send.OkAsync(new PagedResult<DeadLetterDto>(items, totalCount, page, pageSize), ct);
    }
}
