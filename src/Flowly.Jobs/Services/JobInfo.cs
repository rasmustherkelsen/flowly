using Flowly.Jobs.Model;

namespace Flowly.Jobs.Services;

public record JobInfo(
    Guid JobIdentifier,
    string JobTypeName,
    string Description,
    JobState CurrentState,
    DateTimeOffset Created,
    DateTimeOffset? Started,
    DateTimeOffset? Completed,
    string? FaultReason,
    int RetryAttempt);
