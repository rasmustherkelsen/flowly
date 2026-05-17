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

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var converter = new DateTimeOffsetToBinaryConverter();

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.ClrType.GetProperties()
                    .Where(p => p.PropertyType == typeof(DateTimeOffset) || p.PropertyType == typeof(DateTimeOffset?));

                foreach (var property in properties)
                    modelBuilder.Entity(entityType.Name).Property(property.Name).HasConversion(converter);
            }
        }
    }

    public DbSet<Job> Jobs => Set<Job>();
    
    public DbSet<JobAliveStatus> JobAliveStatuses => Set<JobAliveStatus>();

    public DbSet<JobType> JobTypes => Set<JobType>();

    public DbSet<CustomJobState> CustomJobStates => Set<CustomJobState>();
}