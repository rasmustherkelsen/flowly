using System.ComponentModel.DataAnnotations;
using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Model;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.DatabaseModel;

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

    public required DateTimeOffset Created { get; set; }

    public DateTimeOffset? Started { get; set; }

    public DateTimeOffset? Completed { get; set; }

    [MaxLength(1024)]
    public string? FaultReason { get; set; }

    public CustomJobState? CustomJobState { get; set; }
    
    public bool IsRecurringJob { get; set; }

    [MaxLength(100)]
    public string? CronExpression { get; set; }
}