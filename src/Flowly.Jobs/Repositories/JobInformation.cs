namespace Flowly.Jobs.Repositories;

internal record JobInformation(
    Guid JobId,
    string JobTypeName,
    JobState CurrentState,
    string Description,
    DateTimeOffset Created,
    DateTimeOffset? Started,
    DateTimeOffset? Completed,
    string? FaultReason,
    bool IsRecurringJob,
    string? CronExpression,
    string? CustomJobState);