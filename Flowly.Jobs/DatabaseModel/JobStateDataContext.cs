using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Flowly.Jobs.DatabaseModel;

internal class JobStateDataContext(DbContextOptions<JobStateDataContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>()
            .Property(x => x.CurrentState)
            .HasConversion<EnumToStringConverter<JobState>>()
            .HasMaxLength(50);
    }

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<JobType> JobTypes => Set<JobType>();

    public DbSet<CustomJobState> CustomJobStates => Set<CustomJobState>();
}