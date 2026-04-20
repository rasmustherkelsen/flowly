using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.DatabaseModel;

[Index(nameof(Name), IsUnique = true)]
public class JobType
{
    public long Id { get; set; }

    [MaxLength(100)]
    public required string Name { get; set; }

    public List<Job> Jobs { get; set; } = new();
}