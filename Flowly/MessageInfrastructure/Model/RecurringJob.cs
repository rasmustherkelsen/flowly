namespace Flowly.MessageInfrastructure.Model;

internal class RecurringJob
{
    public RecurringJob(
        Guid jobId,
        string jobTypeName,
        TimeSpan interval,
        DateTime created, 
        DateTime? started, 
        DateTime? completed)
    {
        JobId = jobId;
        Created = created;
        Started = started;
        Completed = completed;
        JobTypeName = jobTypeName;
        Interval = interval;
    }

    public Guid JobId { get; }

    public string JobTypeName { get; }
    
    public TimeSpan Interval { get; }

    public DateTime Created { get; }

    public DateTime? Started { get; }

    public DateTime? Completed { get; }

    public bool IsDue()
    {
        if (Started != null && Completed == null)
            return false;

        var timeSinceLastExecution = DateTime.UtcNow - (Completed ?? Created);
        return timeSinceLastExecution > Interval;
    }
}