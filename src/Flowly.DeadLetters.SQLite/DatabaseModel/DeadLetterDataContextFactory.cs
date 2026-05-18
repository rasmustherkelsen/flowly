using System.Diagnostics.CodeAnalysis;
using Flowly.DeadLetters.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowly.DeadLetters.SQLite.DatabaseModel;

[ExcludeFromCodeCoverage]
internal class DeadLetterDataContextFactory : IDesignTimeDbContextFactory<DeadLetterDataContext>
{
    private readonly DeadLetterDataContext _dbContext;

    public DeadLetterDataContextFactory()
    {
        var builder = new DbContextOptionsBuilder<DeadLetterDataContext>();
        builder.UseSqlite(
            "Data Source=flowly-deadletters.db",
            options => options.MigrationsAssembly(typeof(DeadLetterDataContextFactory).Assembly.GetName().Name));
        _dbContext = new DeadLetterDataContext(builder.Options);
    }

    public DeadLetterDataContext CreateDbContext(string[] args) => _dbContext;
}
