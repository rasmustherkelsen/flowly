using System.ComponentModel.DataAnnotations;
using Flowly.Jobs.Model;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.DatabaseModel;

[PrimaryKey(nameof(JobIdentifier))]
public class Job
{
    public required Guid JobIdentifier { get; init; }

    public long JobTypeId { get; set; }

    public JobType? JobType { get; set; }
    
    [MaxLength(200)]
    public required string JobTypeName { get; init; }

    public JobState CurrentState { get; set; } = JobState.Created;

    [MaxLength(200)]
    public required string Description { get; set; }

    public required DateTimeOffset Created { get; set; }

    public DateTimeOffset? Started { get; set; }

    public DateTimeOffset? Completed { get; set; }

    [MaxLength(1024)]
    public string? FaultReason { get; set; }
    
    public bool IsRecurringJob { get; set; }

    [MaxLength(100)]
    public string? CronExpression { get; set; }
}