using Cronos;

namespace Flowly.Jobs.Model;

internal class RecurringJob
{
    public RecurringJob(
        Guid jobId,
        string jobTypeName,
        string description,
        string cronExpression,
        DateTimeOffset created,
        DateTimeOffset? started,
        DateTimeOffset? completed)
    {
        JobId = jobId;
        JobTypeName = jobTypeName;
        Description = description;
        CronExpression = cronExpression;
        Created = created;
        Started = started;
        Completed = completed;
    }

    public Guid JobId { get; }

    public string JobTypeName { get; }

    public string Description { get; }

    public string CronExpression { get; }

    public DateTimeOffset Created { get; }

    public DateTimeOffset? Started { get; }

    public DateTimeOffset? Completed { get; }

    public bool IsDue()
    {
        if (Started != null && Completed == null)
            return false;
        
        var cronExpression = Cronos.CronExpression.Parse(CronExpression, CronExpression.Split(' ').Length == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard);
        var nextOccurrence = cronExpression.GetNextOccurrence((Completed ?? Created).UtcDateTime, TimeZoneInfo.Utc);
        
        return nextOccurrence <= DateTime.UtcNow;
    }
}