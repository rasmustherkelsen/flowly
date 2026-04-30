using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Flowly.DeadLetters.DatabaseModel;

internal class DeadLetterDataContext(DbContextOptions<DeadLetterDataContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeadLetter>()
            .Property(x => x.Status)
            .HasConversion<EnumToStringConverter<DeadLetterStatus>>()
            .HasMaxLength(50);
    }

    public DbSet<DeadLetter> DeadLetters => Set<DeadLetter>();
}
