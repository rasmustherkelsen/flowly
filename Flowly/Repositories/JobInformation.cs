using Flowly.MessageInfrastructure.Model;

namespace Flowly.Repositories;

public record JobInformation(
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