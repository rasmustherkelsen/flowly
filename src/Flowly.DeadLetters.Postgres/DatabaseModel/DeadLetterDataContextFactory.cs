using System.Diagnostics.CodeAnalysis;
using Flowly.DeadLetters.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowly.DeadLetters.Postgres.DatabaseModel;

[ExcludeFromCodeCoverage]
internal class DeadLetterDataContextFactory : IDesignTimeDbContextFactory<DeadLetterDataContext>
{
    private readonly DeadLetterDataContext _dbContext;

    public DeadLetterDataContextFactory()
    {
        var builder = new DbContextOptionsBuilder<DeadLetterDataContext>();
        var connectionString = "Host=localhost;Database=Flowly;Username=postgres;Password=postgres";
        builder.UseNpgsql(connectionString, options =>
        {
            options.EnableRetryOnFailure();
            options.MigrationsAssembly(typeof(DeadLetterDataContextFactory).Assembly.GetName().Name);
        });
        _dbContext = new DeadLetterDataContext(builder.Options);
    }

    public DeadLetterDataContext CreateDbContext(string[] args) => _dbContext;
}
