namespace Flowly.Jobs.Services;

public record RecurringJobInfo(
    Guid JobId,
    string JobTypeName,
    string Description,
    string CronExpression,
    DateTimeOffset Created,
    DateTimeOffset? LastStarted,
    DateTimeOffset? LastCompleted);
