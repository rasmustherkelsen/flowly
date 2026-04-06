using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.DatabaseModel;

[PrimaryKey(nameof(JobIdentifier))]
public class JobAliveStatus
{
    public required Guid JobIdentifier { get; set; }
    
    public required DateTimeOffset LastAliveTimestamp { get; set; }
}