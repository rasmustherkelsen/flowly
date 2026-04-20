using System.Diagnostics.CodeAnalysis;
using Flowly.DeadLetters.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowly.DeadLetters.SqlServer.DatabaseModel;

[ExcludeFromCodeCoverage]
internal class DeadLetterDataContextFactory : IDesignTimeDbContextFactory<DeadLetterDataContext>
{
    private readonly DeadLetterDataContext _dbContext;

    public DeadLetterDataContextFactory()
    {
        var builder = new DbContextOptionsBuilder<DeadLetterDataContext>();
        var connectionString = "Data Source=localhost;Initial Catalog=Flowly;User=sa;Password=Strong01;TrustServerCertificate=True";
        builder.UseSqlServer(connectionString, options =>
        {
            options.EnableRetryOnFailure();
            options.MigrationsAssembly(typeof(DeadLetterDataContextFactory).Assembly.GetName().Name);
        });
        _dbContext = new DeadLetterDataContext(builder.Options);
    }

    public DeadLetterDataContext CreateDbContext(string[] args) => _dbContext;
}
