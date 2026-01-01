using System.ComponentModel.DataAnnotations;
using Flowly.MessageInfrastructure.Model;
using Microsoft.EntityFrameworkCore;

namespace Flowly.DatabaseModel.JobStateDatabase;

[Index(nameof(JobId), IsUnique = true)]
internal class Job
{
    public long Id { get; set; }

    public required Guid JobId { get; init; }

    public long JobTypeId { get; set; }

    public JobType? JobType { get; set; }

    public JobState CurrentState { get; set; } = JobState.Created;

    [MaxLength(200)]
    public required string Description { get; set; }

    public required DateTime Created { get; set; }

    public DateTime? Started { get; set; }

    public DateTime? Completed { get; set; }

    [MaxLength(1024)]
    public string? FaultReason { get; set; }

    public CustomJobState? CustomJobState { get; set; }
    
    public bool IsRecurringJob { get; set; }

    public TimeSpan? Interval { get; set; }
}