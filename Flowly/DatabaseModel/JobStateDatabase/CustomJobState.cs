using Microsoft.EntityFrameworkCore;

namespace Flowly.DatabaseModel.JobStateDatabase;

[Index(nameof(JobId))]
internal class CustomJobState
{
    public long Id { get; set; }

    public long JobId { get; set; }

    public required Guid JobIdentifier { get; set; }

    public Job? Job { get; set; }

    public string? CustomState { get; set; }
}