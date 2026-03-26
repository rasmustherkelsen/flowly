using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Flowly.DeadLetters.DatabaseModel;

[PrimaryKey(nameof(MessageId))]
public class DeadLetter
{
    [MaxLength(1000)]
    public required string MessageId { get; init; }

    [MaxLength(200)]
    public required string QueueName { get; init; }

    public required string MessageBody { get; init; }

    public required string MessageProperties { get; init; }

    public required DateTimeOffset DeadLetteredAt { get; init; }

    [MaxLength(500)]
    public string? DeadLetterReason { get; init; }

    [MaxLength(2000)]
    public string? DeadLetterErrorDescription { get; init; }

    public DeadLetterStatus Status { get; set; } = DeadLetterStatus.Pending;

    public DateTimeOffset? RequeuedAt { get; set; }

    [MaxLength(200)]
    public string? RequeuedBy { get; set; }

}
