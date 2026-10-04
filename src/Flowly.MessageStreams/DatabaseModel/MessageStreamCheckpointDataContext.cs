using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Flowly.MessageStreams.DatabaseModel;

internal class MessageStreamCheckpointDataContext(DbContextOptions<MessageStreamCheckpointDataContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MessageStreamCheckpointRecord>()
            .HasIndex(x => new { x.QueueName, x.ConsumerName, x.Partition })
            .IsUnique();

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            modelBuilder.Entity<MessageStreamCheckpointRecord>()
                .Property(x => x.UpdatedAtUtc)
                .HasConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    public DbSet<MessageStreamCheckpointRecord> Checkpoints => Set<MessageStreamCheckpointRecord>();
}
