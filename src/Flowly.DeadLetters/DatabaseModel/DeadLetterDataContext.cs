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

    public DbSet<DeadLetter> DeadLetters => Set<DeadLetter>();
}
