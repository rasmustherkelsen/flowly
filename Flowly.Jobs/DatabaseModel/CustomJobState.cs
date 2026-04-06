using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.DatabaseModel;

[PrimaryKey(nameof(JobIdentifier))]
public class CustomJobState
{
    public required Guid JobIdentifier { get; set; }

    public string? CustomState { get; set; }
}
