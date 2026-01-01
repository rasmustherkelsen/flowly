using Flowly.MessageInfrastructure.Model;

namespace Flowly.Repositories;

public record JobInformation(
    Guid JobId,
    string JobTypeName,
    JobState CurrentState,
    string Description,
    DateTime Created,
    DateTime? Started,
    DateTime? Completed,
    string? FaultReason,
    bool IsRecurringJob,
    TimeSpan? Interval,
    string? CustomJobState);