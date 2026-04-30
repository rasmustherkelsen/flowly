using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.DatabaseModel;

[PrimaryKey(nameof(JobIdentifier))]
internal class JobAliveStatus
{
    public required Guid JobIdentifier { get; set; }

    public required DateTimeOffset LastAliveTimestamp { get; set; }
}